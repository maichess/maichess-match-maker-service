namespace MaichessMatchMakerService.Queue;

// A player's requested side. ANY (the default) lets the matcher assign; WHITE/BLACK
// request a specific color. For a vs-bot match ANY means a coin flip ("random").
internal enum ColorPreference
{
    Any,
    White,
    Black,
}
