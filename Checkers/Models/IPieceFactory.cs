namespace Checkers.Models;

public interface IPieceFactory
{
    IPiece Create(int id, PieceColor color, PieceType type);
    IPiece CreateMan(int id, PieceColor color);
    IPiece CreateKing(int id, PieceColor color);
}
