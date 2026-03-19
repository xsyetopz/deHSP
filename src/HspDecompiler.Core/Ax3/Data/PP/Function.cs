using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.PP
{
    internal enum FunctionType
    {
        NULL = 0x00,
        func = 0x01,
        cfunc = 0x02,
        deffunc = 0x03,
        defcfunc = 0x04,
        comfunc = 0x05,
        module = 0x06,
    }

    internal enum FunctionFlags
    {
        NULL = 0,
        onexit = 0x01,
    }

    class Function : Preprocessor
    {
        // Fix #32: named constants for dllIndex/functionIndex/labelIndex sentinel values
        private const int DllIndexDeffunc = -1;
        private const int DllIndexDefcfunc = -2;
        private const int DllIndexModule = -3;
        private const int FuncIndexComfunc = -7;
        private const int LabelIndexFunc1 = 2;
        private const int LabelIndexOnExit = 3;
        private const int LabelIndexCfunc = 4;
        private const int LabelIndexFunc2 = 6;

        private Function() { }
        private Function(int index) : base(index) { }
        private int dllIndex;
        private int functionIndex;

        List<Param> functionParams = new List<Param>();
        int strIndex;
        private int paramSizeSum;
        private int labelIndex;
        private Int16 int_0;
        private int flags;

        internal static Function FromBinaryReader(BinaryReader reader, AxData parent, int index)
        {
            Function ret = new Function(index);
            ret.dllIndex = reader.ReadInt16();
            ret.functionIndex = reader.ReadInt16();

            int paramStart = reader.ReadInt32();
            int paramCount = reader.ReadInt32();
            if (paramCount != 0)
            {
                ret.functionParams = parent.FunctionParams.GetRange(paramStart, paramCount);
            }

            ret.strIndex = reader.ReadInt32();
            if (ret.strIndex >= 0)
            {
                ret.defaultName = parent.ReadStringLiteral(ret.strIndex);
            }

            ret.paramSizeSum = reader.ReadInt32();
            ret.labelIndex = reader.ReadInt32();

            ret.int_0 = reader.ReadInt16();
            ret.flags = reader.ReadInt16();
            switch (ret.Type)
            {
                case FunctionType.defcfunc:
                case FunctionType.deffunc:
                    Label label = parent.GetLabel(ret.labelIndex);
                    if (label != null)
                    {
                        label.SetFunction(ret);
                    }

                    ret.label = label;
                    break;

                case FunctionType.func:
                case FunctionType.cfunc:
                case FunctionType.comfunc:
                    Usedll? dll = parent.GetUsedll(ret.dllIndex);
                    if (dll != null)
                    {
                        dll.AddFunction(ret);
                    }

                    ret.dll = dll;
                    break;
                case FunctionType.module:
                    parent.Modules.Add(ret);
                    break;
            }
            return ret;
        }

        internal bool IsModuleFunction
        {
            get
            {
                return Type == FunctionType.module;
            }
        }
        internal bool IsComFunction
        {
            get
            {
                return Type == FunctionType.comfunc;
            }
        }
        internal bool IsUserFunction
        {
            get
            {
                switch (Type)
                {
                    case FunctionType.deffunc:
                    case FunctionType.defcfunc:
                        return true;
                }
                return false;
            }
        }

        internal bool IsDllFunction
        {
            get
            {
                switch (Type)
                {
                    case FunctionType.func:
                    case FunctionType.cfunc:
                        return true;
                }
                return false;
            }
        }

        private string? defaultName = null;

        internal string? DefaultName
        {
            get { return defaultName; }
        }

        internal Function? ParentModule
        {
            get
            {
                if (functionParams.Count == 0)
                {
                    return null;
                }

                if (!functionParams[0].IsModuleType)
                {
                    return null;
                }

                return functionParams[0].Module;
            }
        }
        private string? name = null;
        private Label? label = null;
        private Usedll? dll = null;

        // Fix #32: use named sentinel constants instead of magic numbers
        internal FunctionType Type
        {
            get
            {
                if (dllIndex == DllIndexDeffunc)
                {
                    return FunctionType.deffunc;
                }

                if (dllIndex == DllIndexDefcfunc)
                {
                    return FunctionType.defcfunc;
                }

                if (dllIndex == DllIndexModule)
                {
                    return FunctionType.module;
                }

                if (dllIndex >= 0)
                {
                    if (functionIndex == FuncIndexComfunc)
                    {
                        return FunctionType.comfunc;
                    }

                    if (labelIndex == LabelIndexFunc1 || labelIndex == LabelIndexFunc2)
                    {
                        return FunctionType.func;
                    }

                    if (labelIndex == LabelIndexOnExit)
                    {
                        return FunctionType.func;
                    }

                    if (labelIndex == LabelIndexCfunc)
                    {
                        return FunctionType.cfunc;
                    }
                }
                return FunctionType.NULL;
            }
        }
        internal FunctionFlags Flags
        {
            get
            {
                if ((flags == 1) && (dllIndex == DllIndexDeffunc))
                {
                    return FunctionFlags.onexit;
                }

                if ((dllIndex >= 0) && (labelIndex == LabelIndexOnExit))
                {
                    return FunctionFlags.onexit;
                }

                return FunctionFlags.NULL;
            }
        }
        internal void SetName(string name)
        {
            this.name = name;
        }

        internal string? FunctionName
        {
            get
            {
                if (name != null)
                {
                    return name;
                }

                if (defaultName == null)
                {
                    if (Type == FunctionType.comfunc)
                    {
                        return "comfunc_" + index.ToString();
                    }

                    return null;
                }
                switch (Type)
                {
                    case FunctionType.defcfunc:
                    case FunctionType.deffunc:
                    case FunctionType.module:
                        return defaultName;
                    case FunctionType.func:
                    case FunctionType.cfunc:
                        if (name != null)
                        {
                            return name;
                        }

                        return defaultName;
                    case FunctionType.comfunc:
                        return "comfunc_" + index.ToString();
                    default:
                        break;
                }
                return null;
            }
        }

        private string modFunctionToString()
        {
            StringBuilder strBld = new StringBuilder();
            switch (defaultName)
            {
                case "__init":
                    strBld.Append("#modinit");
                    break;
                case "__term":
                    strBld.Append("#modterm");
                    break;
                default:
                    strBld.Append("#modfunc");
                    strBld.Append(' ');
                    strBld.Append(FunctionName);
                    break;
            }
            if (functionParams.Count > 1)
            {
                for (int i = 1; i < functionParams.Count; i++)
                {
                    if (i != 1)
                    {
                        strBld.Append(',');
                    }

                    strBld.Append(' ');
                    strBld.Append(functionParams[i].ToString());
                }
            }
            return strBld.ToString();
        }

        private string moduleToString(bool useModuleStyle)
        {
            StringBuilder strBld = new StringBuilder();
            if (useModuleStyle)
            {
                strBld.Append("#module ");
                strBld.Append(FunctionName);
            }
            else
            {
                strBld.Append("#struct ");
                strBld.Append(FunctionName);
            }
            if (functionParams.Count > 1)
            {
                for (int i = 1; i < functionParams.Count; i++)
                {
                    if (i != 1)
                    {
                        strBld.Append(',');
                    }

                    strBld.Append(' ');
                    if (useModuleStyle)
                    {
                        strBld.Append(functionParams[i].ToString(true, true, true));
                    }
                    else
                    {
                        strBld.Append(functionParams[i].ToString(true, false, true));
                    }
                }
            }
            return strBld.ToString();
        }

        // Fix #11: extracted parameter-formatting logic into AppendParams helper
        private void AppendParams(StringBuilder strBld, int paramStart)
        {
            if (functionParams.Count > paramStart)
            {
                for (int i = paramStart; i < functionParams.Count; i++)
                {
                    if (i != paramStart)
                    {
                        strBld.Append(',');
                    }

                    strBld.Append(' ');
                    strBld.Append(functionParams[i].ToString());
                }
            }
        }

        internal string ToString(bool useModuleStyle)
        {
            StringBuilder strBld = new StringBuilder();

            int paramStart = 0;
            switch (Type)
            {
                case FunctionType.defcfunc:
                    strBld.Append("#defcfunc ");
                    strBld.Append(FunctionName);
                    break;
                case FunctionType.module:
                    return moduleToString(useModuleStyle);
                case FunctionType.deffunc:
                    if (useModuleStyle)
                    {
                        if ((functionParams.Count != 0) && (functionParams[0].IsModuleType))
                        {
                            return modFunctionToString();
                        }
                    }

                    strBld.Append("#deffunc ");
                    strBld.Append(FunctionName);
                    if ((Flags & FunctionFlags.onexit) == FunctionFlags.onexit)
                    {
                        strBld.Append(" onexit");
                    }

                    break;
                case FunctionType.func:
                    strBld.Append("#func ");
                    strBld.Append(FunctionName);
                    strBld.Append(' ');
                    if ((Flags & FunctionFlags.onexit) == FunctionFlags.onexit)
                    {
                        strBld.Append("onexit ");
                    }

                    strBld.Append('"');
                    strBld.Append(defaultName);
                    strBld.Append('"');
                    break;
                case FunctionType.cfunc:
                    strBld.Append("#cfunc ");
                    strBld.Append(FunctionName);
                    strBld.Append(@" """);
                    strBld.Append(defaultName);
                    strBld.Append('"');
                    break;
                case FunctionType.comfunc:
                    strBld.Append("#comfunc ");
                    strBld.Append(FunctionName);
                    strBld.Append(' ');
                    strBld.Append(labelIndex.ToString());
                    paramStart = 1;
                    break;
                default:
                    return "/*#deffunc?*/";
            }
            AppendParams(strBld, paramStart);
            return strBld.ToString();
        }

        public override string ToString()
        {
            return ToString(false);
        }
    }
}
