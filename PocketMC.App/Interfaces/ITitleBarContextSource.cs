namespace PocketMC.App.Interfaces;

public interface ITitleBarContextSource
{
    string? TitleBarTitle { get; }
    string? TitleBarSubtitle { get; }
    string? TitleBarStatusDotColor { get; }
}
