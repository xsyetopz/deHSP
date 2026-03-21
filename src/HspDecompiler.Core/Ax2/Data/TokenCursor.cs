namespace HspDecompiler.Core.Ax2.Data;

internal class TokenCursor
{
    private readonly AxData _data;
    private int _nextOffset;
    private int _index;
    private Token? _nextToken;

    internal TokenCursor(AxData data)
    {
        _data = data;
    }

    internal int Index => _index;

    internal int Percent => (int)((double)_nextOffset * 100.0 / _data.TokenData.Length);

    internal void SetZero()
    {
        _index = 0;
        _nextOffset = 0;
        _nextToken = Token.GetToken(_data, 0);
        _nextOffset += _nextToken!.Size;
    }

    internal Token? GetNext()
    {
        Token? ret = _nextToken;

        _nextToken = Token.GetToken(_data, _nextOffset);
        if (ret == null)
        {
            _index = _nextOffset / 2;
            return null;
        }
        _index = ret.Id;
        if ((_nextToken == null) || (_nextToken.IsLinehead))
        {
            ret._isLineend = true;
        }

        if (_nextToken != null)
        {
            _nextOffset += _nextToken.Size;
            if (ret.NextIsUnenableLabel)
            {
                ret._isLineend = false;
                _ = GetNext();
                if ((_nextToken == null) || (_nextToken.IsLinehead))
                {
                    ret._isLineend = true;
                }

                return ret;
            }
        }
        return ret;
    }
}
