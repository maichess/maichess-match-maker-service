namespace MaichessMatchMakerService.Queue;

// Resolves which side each player takes once a pair has been selected. Color
// preference never affects *whether* two players are paired (admissibility /
// skill / FIFO decide that) — only the side assignment afterwards. See task 21's
// matching rules.
internal static class ColorAssignment
{
    // Which of two paired humans takes White. `coinFlipFirstWhite` is consulted only
    // when the choice is otherwise ambiguous — i.e. both asked for the same fixed
    // color and one must concede. Rules:
    //   - opposite fixed colors        → each gets their wish
    //   - one fixed, the other ANY     → the fixed side gets its color
    //   - both ANY                     → first (longest-waiting) takes White, as before
    //   - both the same fixed color    → coin flip decides who concedes
    internal static bool FirstIsWhite(ColorPreference first, ColorPreference second, bool coinFlipFirstWhite)
    {
        bool firstWhite = first == ColorPreference.White;
        bool firstBlack = first == ColorPreference.Black;
        bool secondWhite = second == ColorPreference.White;
        bool secondBlack = second == ColorPreference.Black;

        if (firstWhite && !secondWhite)
        {
            return true;
        }

        if (firstBlack && !secondBlack)
        {
            return false;
        }

        if (secondWhite && !firstWhite)
        {
            return false;
        }

        if (secondBlack && !firstBlack)
        {
            return true;
        }

        // Remaining: both ANY, or both the same fixed color.
        return first == second && first != ColorPreference.Any ? coinFlipFirstWhite : true;
    }

    // Which side a human takes against a bot. ANY ("random") defers to the coin flip.
    internal static bool HumanIsWhite(ColorPreference human, bool coinFlip) => human switch
    {
        ColorPreference.White => true,
        ColorPreference.Black => false,
        _ => coinFlip,
    };
}
