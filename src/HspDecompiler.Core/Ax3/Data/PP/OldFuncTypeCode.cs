using System.Collections.Generic;

namespace HspDecompiler.Core.Ax3.Data.PP;

// Rebuilds the numeric type code of the old #func form (#func name "name" 6). hspcmp marks such a
// declaration with STRUCTPRM_SUBID_OLDDLL, and only the numeric form brings that mark back, so the
// code is the inverse of the mapping in GenerateCodePP_func of hspcmp/codegen.cpp.
//
// 古い #func 形式（#func name "name" 6）の数値の型コードを組み立て直す。hspcmp はこの宣言に
// STRUCTPRM_SUBID_OLDDLL の印を付け、その印を戻せるのは数値形式だけなので、hspcmp/codegen.cpp の
// GenerateCodePP_func の対応を逆にたどる。
internal static class OldFuncTypeCode
{
    private const short IntNumber = 4;
    private const short Int64Number = 8;
    private const short LocalString = -6;
    private const short PVal = -8;
    private const short BmScr = -9;
    private const short VarPointer = -10;
    private const short RefStr = -16;
    private const short ExInfo = -17;
    private const short DpmInfo = -18;
    private const short NullPointer = -19;

    private const int VarFlag = 0x01;
    private const int BmScrFlag = 0x02;
    private const int PValFlag = 0x83;
    private const int StringFlag = 0x04;
    private const int RefStrFlag = 0x10;
    private const int DpmInfoFlag = 0x20;
    private const int ExInfoFlag = 0x202;

    internal static bool TryEncode(IReadOnlyList<Param> parameters, out int code)
    {
        code = 0;
        if (parameters.Count != 4)
        {
            return false;
        }

        short p1 = parameters[0].TypeCode;
        short p2 = parameters[1].TypeCode;
        short p3 = parameters[2].TypeCode;
        short p4 = parameters[3].TypeCode;
        if (p1 == ExInfo)
        {
            return TryEncodeExInfo(p2, p3, p4, out code);
        }

        if (
            !TryEncodeFirst(p1, out int first)
            || !TryEncodeSecond(p2, out int second)
            || !IsInt(p3)
            || !TryEncodeLast(p4, out int last)
        )
        {
            return false;
        }

        code = first | second | last;
        return true;
    }

    // hspcmp 3.8β2 writes int64 into the int slots, earlier versions int.
    //
    // hspcmp 3.8β2 は int の枠に int64 を、それより前の版は int を書く。
    private static bool IsInt(short type) => type is IntNumber or Int64Number;

    private static bool TryEncodeExInfo(short p2, short p3, short p4, out int code)
    {
        code = ExInfoFlag;
        if (p2 != NullPointer || p3 != NullPointer)
        {
            return false;
        }

        switch (p4)
        {
            case NullPointer:
                return true;
            case RefStr:
                code |= RefStrFlag;
                return true;
            case DpmInfo:
                code |= DpmInfoFlag;
                return true;
            default:
                return false;
        }
    }

    private static bool TryEncodeFirst(short type, out int flag)
    {
        flag = type switch
        {
            VarPointer => VarFlag,
            BmScr => BmScrFlag,
            PVal => PValFlag,
            _ => 0,
        };
        return flag != 0 || IsInt(type);
    }

    private static bool TryEncodeSecond(short type, out int flag)
    {
        flag = type == LocalString ? StringFlag : 0;
        return flag != 0 || IsInt(type);
    }

    private static bool TryEncodeLast(short type, out int flag)
    {
        flag = type switch
        {
            RefStr => RefStrFlag,
            DpmInfo => DpmInfoFlag,
            _ => 0,
        };
        return flag != 0 || IsInt(type);
    }
}
