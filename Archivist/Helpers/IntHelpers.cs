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

        internal static string NumberOrNo(this int value, string noString = "no")
        {
            return value == 0
                ? noString
                : value.ToString();
        }
    }
}
