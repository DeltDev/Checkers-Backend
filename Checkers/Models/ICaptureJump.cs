namespace Checkers.Models;

public interface ICaptureJump
{
    Position Victim { get; set; }
    Position Landing { get; set; }
}