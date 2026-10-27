namespace Checkers.Models;

public sealed class PieceFactory : IPieceFactory
{
    public IPiece Create(int id, PieceColor color, PieceType type)
    {
        return new Piece(id, color, type);
    }

    public IPiece CreateMan(int id, PieceColor color)
    {
        return Create(id, color, PieceType.Man);
    }

    public IPiece CreateKing(int id, PieceColor color)
    {
        return Create(id, color, PieceType.King);
    }
}
