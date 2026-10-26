namespace Checkers.Models;

public class CaptureJump : ICaptureJump
{
    public Position Victim { get; set; }
    public Position Landing { get; set; }
}