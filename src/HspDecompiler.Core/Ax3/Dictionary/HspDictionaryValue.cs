using System;

namespace HspDecompiler.Core.Ax3.Dictionary
{
    internal struct HspDictionaryValue
    {
        internal HspDictionaryValue(string theName, string theType, string[] theExtras)
        {
            Name = theName;
            Type = (HspCodeType)Enum.Parse(typeof(HspCodeType), theType);
            Extra = HspCodeExtraFlags.NONE;
            OperatorPriority = -1;
            foreach (string theExtra in theExtras)
            {
                string testString = theExtra.Trim();
                if (testString.Length == 0)
                {
                    continue;
                }

                if (testString.StartsWith("Priority_", StringComparison.Ordinal))
                {
                    OperatorPriority = int.Parse(testString.Substring(9));
                    continue;
                }
                Extra |= (HspCodeExtraFlags)Enum.Parse(typeof(HspCodeExtraFlags), testString);
            }
        }

        internal string Name;
        internal HspCodeType Type;
        internal HspCodeExtraFlags Extra;
        internal int OperatorPriority;

        public override string ToString()
        {
            if (Name.Length == 0)
            {
                return Type.ToString();
            }

            return Type.ToString() + "  \"" + Name + "\"";
        }
    }
}
