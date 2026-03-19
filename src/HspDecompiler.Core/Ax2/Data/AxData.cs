using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax2.Data
{
    internal class AxData
    {
        internal AxData()
        {
        }

        private Header? header;
        private Label[]? labels;
        private Dll[]? dlls;
        private Func[]? funcs;
        private Deffunc[]? deffuncs;
        private Module[]? modules;

        private byte[]? labelData;
        private byte[]? dllData;
        private byte[]? funcData;
        private byte[]? deffuncData;
        private byte[]? moduleData;
        private byte[]? tokenData;

        internal byte[] TokenData
        {
            get
            {
                return tokenData!;
            }
        }

        private byte[]? stringData;

        private void ReadData(Stream stream)
        {
            long startPosition = stream.Position;
            byte[] headerBuffer = new byte[80];
            if (stream.Read(headerBuffer, 0, 80) < 80)
            {
                throw new HspDecoderException("AxData", Strings.FileHeaderMissing);
            }

            int[] buffer = new int[20];
            for (int i = 0; i < 20; i++)
            {
                buffer[i] = BitConverter.ToInt32(headerBuffer, i * 4);
            }
            try
            {
                header = Header.FromIntArray(buffer);
            }
            catch (Exception e)
            {
                throw new HspDecoderException("AxHeader", Strings.UnexpectedErrorDuringHeaderAnalysis, e);
            }
            if (header == null)
            {
                throw new HspDecoderException("AxHeader", Strings.HeaderAnalysisFailed);
            }

            try
            {
                Header head = header;
                tokenData = new byte[head.ScriptByte];
                stream.Seek(startPosition + head.ScriptOffset, SeekOrigin.Begin);
                stream.ReadExactly(tokenData, 0, head.ScriptByte);

                dllData = new byte[head.DllByte];
                stream.Seek(startPosition + head.DllOffset, SeekOrigin.Begin);
                stream.ReadExactly(dllData, 0, head.DllByte);

                funcData = new byte[head.FuncByte];
                stream.Seek(startPosition + head.FuncOffset, SeekOrigin.Begin);
                stream.ReadExactly(funcData, 0, head.FuncByte);

                deffuncData = new byte[head.DeffuncByte];
                stream.Seek(startPosition + head.DeffuncOffset, SeekOrigin.Begin);
                stream.ReadExactly(deffuncData, 0, head.DeffuncByte);

                moduleData = new byte[head.ModuleByte];
                stream.Seek(startPosition + head.ModuleOffset, SeekOrigin.Begin);
                stream.ReadExactly(moduleData, 0, head.ModuleByte);

                labelData = new byte[head.LabelByte];
                stream.Seek(startPosition + head.LabelOffset, SeekOrigin.Begin);
                stream.ReadExactly(labelData, 0, head.LabelByte);

                stringData = new byte[head.TextByte];
                stream.Seek(startPosition + head.TextOffset, SeekOrigin.Begin);
                stream.ReadExactly(stringData, 0, head.TextByte);
            }
            catch (Exception e)
            {
                throw new HspDecoderException("AxHeader", Strings.UnexpectedErrorDuringStreamRead, e);
            }
            stream.Seek(startPosition, SeekOrigin.Begin);
        }

        #region create
        internal static AxData FromStream(Stream stream)
        {
            AxData data = new AxData();
            data.ReadData(stream);
            return data;
        }
        #endregion

        #region read
        internal string GetString(int offset)
        {
            return ReadString(offset, stringData!);
        }

        private string ReadString(int offset)
        {
            return ReadString(offset, stringData!);
        }

        private string ReadString(int offset, byte[] dumpData)
        {
            System.Text.Encoding encode = HspDecompiler.Core.Encoding.ShiftJisHelper.Encoding;
            List<byte> buffer = new List<byte>();
            byte token;
            while (offset < dumpData.Length)
            {
                token = dumpData[offset];
                offset++;
                if (token == 0)
                {
                    break;
                }

                buffer.Add(token);
            }
            if (buffer.Count == 0)
            {
                return "";
            }

            byte[] bytes = new byte[buffer.Count];
            buffer.CopyTo(bytes);
            return encode.GetString(bytes);
        }

        private void ReadLabels()
        {
            labels = new Label[header!.LabelCount];
            for (int i = 0; i < header.LabelCount; i++)
            {
                int offset = i * 4;
                labels[i] = new Label(i, BitConverter.ToInt32(labelData!, offset));
            }
        }

        private void ReadDlls()
        {
            dlls = new Dll[header!.DllCount];

            for (int i = 0; i < header.DllCount; i++)
            {
                int offset = 4 + (i * 24);
                dlls[i].Name = ReadString(offset, dllData!);
            }
        }

        private void ReadFuncs()
        {
            funcs = new Func[header!.FuncCount];
            for (int i = 0; i < header.FuncCount; i++)
            {
                int offset = i * 16;
                funcs[i].DllIndex = BitConverter.ToInt16(funcData!, offset);
                offset += 4;
                funcs[i].HikiType = BitConverter.ToInt16(funcData!, offset);
                offset += 4;
                int funcnameOffset = BitConverter.ToInt32(funcData!, offset);
                funcs[i].Name = ReadString(funcnameOffset);
            }
        }

        private void ReadModules()
        {
            if (header!.ModuleCount == 0)
            {
                return;
            }

            modules = new Module[header.ModuleCount];

            for (int i = 0; i < header.ModuleCount; i++)
            {
                int offset = 4 + (i * 24);
                modules[i].Name = ReadString(offset, dllData!);
            }
        }

        private void ReadDeffuncs()
        {
            deffuncs = new Deffunc[header!.DeffuncCount];

            for (int i = 0; i < header.DeffuncCount; i++)
            {
                int offset = i * 16;
                int labelIndex = BitConverter.ToInt32(deffuncData!, offset) - 0x1000;
                labels![labelIndex].Deffunc = i;

                offset += 4;
                deffuncs[i].HikiType = BitConverter.ToInt16(deffuncData!, offset);
                offset += 2;
                deffuncs[i].HikiCount = BitConverter.ToInt16(deffuncData!, offset);
                offset += 2;
                int deffuncnameOffset = BitConverter.ToInt32(deffuncData!, offset);
                deffuncs[i].Name = ReadString(deffuncnameOffset);
                labels[labelIndex].Name = deffuncs[i].ToString();
            }
        }

        #endregion

        List<string> lines = new List<string>();

        internal void Decompile()
        {
            Token.CurrentData = this;
            lines.Clear();

            ReadLabels();
            ReadDlls();
            ReadFuncs();
            ReadModules();
            ReadDeffuncs();

            if (dlls != null)
            {
                for (int i = 0; i < dlls.Length; i++)
                {
                    lines.Add(dlls[i].ToString());
                    if (funcs != null)
                    {
                        for (int j = 0; j < funcs.Length; j++)
                        {
                            if (funcs[j].DllIndex == i)
                            {
                                lines.Add(funcs[j].ToString());
                            }
                        }
                    }
                }
            }

            Token.SetZero();
            Token? token;
            try
            {
                while ((token = Token.GetNext()) != null)
                {
                    if (token.LabelIndex != -1)
                    {
                        labels![token.LabelIndex].LoadCount += 1;
                    }
                }
            }
            catch (Exception e)
            {
                throw new HspDecoderException("AxHeader", Strings.UnrecoverableErrorDuringLabelRead, e);
            }

            for (int i = 0; i < labels!.Length; i++)
            {
                labels[i].Enabled = labels[i].LoadCount > 0;
            }

            string? line;
            Token.SetZero();
            while ((line = GetLine()) != null)
            {
                lines.Add(line);
            }

            return;
        }

        private void AddLabel()
        {
            for (int i = 0; i < labels!.Length; i++)
            {
                if (!labels[i].Enabled)
                {
                    continue;
                }

                if (Token.Index >= labels[i].TokenIndex)
                {
                    lines.Add(labels[i].ToString() ?? string.Empty);
                    labels[i].Enabled = false;
                }
            }
        }

        private string GetTab(int tab)
        {
            string ret = "";
            Debug.Assert(tab >= 0);
            for (int i = 0; i < tab; i++)
            {
                ret += "\t";
            }
            return ret;
        }

        private int tabNo = 1;
        private List<int> ifEnd = new List<int>();

        private void EmitLabel(Token token)
        {
            for (int i = 0; i < labels!.Length; i++)
            {
                if (!labels[i].Enabled)
                {
                    continue;
                }

                if (token.Id == labels[i].TokenIndex)
                {
                    lines.Add(labels[i].Name);
                    labels[i].Enabled = false;
                }
            }
        }

        private string FormatTokenOutput(Token first)
        {
            string line = GetTab(tabNo) + first.GetString();

            if (first.isLineend)
            {
                return line;
            }

            Token? token;
            while ((token = Token.GetNext()) != null)
            {
                string add = token.GetString();
                line += token.isArg ? ", " : " ";
                line += add;
                if (token.isLineend)
                {
                    break;
                }
            }
            return line;
        }

        private string? GetLine()
        {
            Token? token = Token.GetNext();
            if (token == null)
            {
                return null;
            }

            for (int i = 0; i < ifEnd.Count; i++)
            {
                if ((token.Id == ifEnd[i]) || (token.IfJumpId == ifEnd[i]))
                {
                    tabNo--;
                    lines.Add(GetTab(tabNo) + "}");
                    ifEnd.RemoveAt(i);
                    i--;
                }
            }

            EmitLabel(token);

            bool tabPlus = token.TabPlus;
            int ifJumpTo = token.IfJumpTo;
            if (token.TabMinus)
            {
                tabNo--;
            }

            string line = FormatTokenOutput(token);

            if (tabPlus)
            {
                tabNo++;
            }

            if (ifJumpTo >= 0)
            {
                line += " {";
                ifEnd.Add(ifJumpTo);
            }
            return line;
        }

        internal string? GetDeffuncName(int index)
        {
            if (deffuncs == null || (index >= deffuncs.Length) || (index < 0))
            {
                return null;
            }

            return deffuncs[index].Name;
        }

        internal string? GetFuncName(int index)
        {
            if (funcs == null || (index >= funcs.Length) || (index < 0))
            {
                return null;
            }

            return funcs[index].Name;
        }

        internal List<string> GetLines()
        {
            return lines;
        }
    }
}
