﻿#region License
/* 
 * Copyright (C) 1999-2016 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Reko.Core.Serialization
{
    public class ProjectSaver : ProjectPersister
    {
        public ProjectSaver(IServiceProvider services) : base(services)
        {
        }

        public void Save(string projectAbsPath, Project project, TextWriter sw)
        {
            var sProject = Save(projectAbsPath, project);
            Save(sProject, sw);
        }

        public void Save(Project_v4 sProject, TextWriter sw)
        {
            var ser = SerializedLibrary.CreateSerializer_v4(typeof(Project_v4));
            ser.Serialize(sw, sProject);
        }

        public Project_v4 Save(string projectAbsPath, Project project)
        {
            var inputs = new List<ProjectFile_v3>();
            inputs.AddRange(project.Programs.Select(p => VisitProgram(projectAbsPath, p)));
            inputs.AddRange(project.MetadataFiles.Select(m => VisitMetadataFile(projectAbsPath, m)));
            var sp = new Project_v4
            {
                // ".Single()" because there can be only one Architecture and Platform, realistically.
                ArchitectureName = project.Programs.Select(p => p.Architecture.Name).Distinct().SingleOrDefault(),
                PlatformName = project.Programs.Select(p => p.Platform.Name).Distinct().SingleOrDefault(),   
                Inputs = inputs
            };
            return sp;
        }

        public ProjectFile_v3 VisitProgram(string projectAbsPath, Program program)
        {
            var dtSerializer = new DataTypeSerializer();
            return new DecompilerInput_v4
            {
                Filename = ConvertToProjectRelativePath(projectAbsPath, program.Filename),
                User = new UserData_v4
                {
                    Procedures = program.User.Procedures
                        .Select(de => { de.Value.Address = de.Key.ToString(); return de.Value; })
                        .ToList(),
                    Processor = SerializeProcessorOptions(program.User, program.Architecture),
                    PlatformOptions = SerializePlatformOptions(program.User, program.Platform),
                    LoadAddress = program.User.LoadAddress != null ? program.User.LoadAddress.ToString() : null,
                    Calls = program.User.Calls
                        .Select(uc => SerializeUserCall(program, uc.Value))
                        .Where(uc => uc != null)
                        .ToList(),
                    GlobalData = program.User.Globals
                        .Select(de => new GlobalDataItem_v2
                        {
                            Address = de.Key.ToString(),
                            DataType = de.Value.DataType,
                            Name = string.Format("g_{0:X}", de.Key.ToLinear())
                        })
                        .ToList(),
                    OnLoadedScript = program.User.OnLoadedScript,
                    Heuristics = program.User.Heuristics
                        .Select(h => new Heuristic_v3 { Name = h }).ToList(),
                    Annotations = program.User.Annotations.Select(SerializeAnnotation).ToList(),
                    TextEncoding = program.User.TextEncoding != Encoding.ASCII ? program.User.TextEncoding.WebName : null,
                },
                DisassemblyFilename =  ConvertToProjectRelativePath(projectAbsPath, program.DisassemblyFilename),
                IntermediateFilename = ConvertToProjectRelativePath(projectAbsPath, program.IntermediateFilename),
                OutputFilename =       ConvertToProjectRelativePath(projectAbsPath, program.OutputFilename),
                TypesFilename =        ConvertToProjectRelativePath(projectAbsPath, program.TypesFilename),
                GlobalsFilename =      ConvertToProjectRelativePath(projectAbsPath, program.GlobalsFilename),
            };
        }

        private SerializedCall_v1 SerializeUserCall(Program program, UserCallData uc)
        {
            if (uc == null || uc.Address == null)
                return null;
            var procser = program.CreateProcedureSerializer();
            var ssig = procser.Serialize(uc.Signature);
            return new SerializedCall_v1
            {
                InstructionAddress = uc.Address.ToString(),
                Comment = uc.Comment,
                NoReturn = uc.NoReturn,
                Signature = ssig,
            };
        }

        private Annotation_v3 SerializeAnnotation(Annotation arg)
        {
            return new Annotation_v3
            {
                Address = arg.Address.ToString(),
                Text = arg.Text,
            };
        }

        private ProcessorOptions_v4 SerializeProcessorOptions(UserData user, IProcessorArchitecture architecture)
        {
            if (architecture == null)
                return null;
            var options = architecture.SaveUserOptions();
            if (string.IsNullOrEmpty(user.Processor) && options == null)
                return null;
            else
            {
                var doc = new XmlDocument();
                return new ProcessorOptions_v4 {
                    Name = user.Processor,
                    Options = SerializeValue(options, doc)
                        .ChildNodes
                        .OfType<XmlElement>()
                        .ToArray()
                };
            }
        }

        private PlatformOptions_v4 SerializePlatformOptions(UserData user, IPlatform platform)
        {
            if (platform == null)
                return null;
            var dictionary = platform.SaveUserOptions();
            if (dictionary == null)
            {
                if (string.IsNullOrEmpty(user.Environment))
                    return null;
                else
                    return new PlatformOptions_v4
                    {
                        Name = user.Environment
                    };
            }
            var doc = new XmlDocument();
            return new PlatformOptions_v4
            {
                Name = user.Environment,
                Options = SerializeValue(dictionary, doc)
                    .ChildNodes
                    .OfType<XmlElement>()
                    .ToArray()
            };
        }

        private XmlElement SerializeOptionValue(string key, object value, XmlDocument doc)
        {
            var el = SerializeValue(value, doc);
            el.SetAttribute("key", "", key);
            return el;
        }

        private XmlElement SerializeValue(object value, XmlDocument doc)
        {
            var sValue = value as string;
            if (sValue != null)
            {
                var el = doc.CreateElement("item", SerializedLibrary.Namespace_v4);
                el.InnerXml = (string)value;
                return el;
            }
            var dict = value as IDictionary;
            if (dict != null)
            {
                var el = doc.CreateElement("dict", SerializedLibrary.Namespace_v4);
                foreach (DictionaryEntry de in dict)
                {
                    var sub = SerializeValue(de.Value, doc);
                    sub.SetAttribute("key", de.Key.ToString());
                    el.AppendChild(sub);
                }
                return el;
            }
            var ienum = value as IEnumerable;
            if (ienum != null)
            {
                var el = doc.CreateElement("list", SerializedLibrary.Namespace_v4);
                foreach (var oValue in ienum)
                {
                    el.AppendChild(SerializeValue(oValue, doc));
                }
                return el;
            }
            throw new NotSupportedException(typeof(object).Name);
        }

        public ProjectFile_v3 VisitMetadataFile(string projectAbsPath, MetadataFile metadata)
        {
            return new MetadataFile_v3
            {
                 Filename = ConvertToProjectRelativePath(projectAbsPath, metadata.Filename),
                  ModuleName = metadata.ModuleName,
            };
        }
    }
}
