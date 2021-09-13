namespace Archivist.Helpers
{
    internal static class IntHelpers
    {
        internal static string PluralSuffix(this int number)
        {
            return number == 1
                ? ""
                : "s";
        }
    }
}
