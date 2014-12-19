﻿#region License
/* 
 * Copyright (C) 1999-2014 John Källén.
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

using Decompiler.Core;
using Decompiler.Core.Expressions;
using Decompiler.Core.Machine;
using Decompiler.Core.Operators;
using Decompiler.Core.Rtl;
using Decompiler.Core.Serialization;
using Decompiler.Core.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Decompiler.Arch.X86
{
    public partial class X86Rewriter
    {
        public void RewritePxor()
        {
            var rdst = instrCur.op1 as RegisterOperand;
            var rsrc = instrCur.op2 as RegisterOperand;
            if (rdst != null && rsrc != null && rdst.Register.Number == rsrc.Register.Number)
            { // selfie!
                emitter.Assign(orw.AluRegister(rdst), emitter.Cast(rdst.Width, Constant.Int32(0)));
                return;
            }
            throw new NotImplementedException();
        }
    }
}