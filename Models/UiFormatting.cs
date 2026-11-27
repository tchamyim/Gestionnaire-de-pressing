using System.Globalization;

namespace Pressings.Models;

public static class UiFormatting
{
    public static string Fcfa(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")) + " FCFA";
}
