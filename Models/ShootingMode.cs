namespace OptishotV1DOTNET.Models;

public enum ShootingMode
{
    Aesthetic,
    Product,
    Portrait,
    Food
}

public static class ShootingModeExtensions
{
    public static string DisplayName(this ShootingMode mode) => mode switch
    {
        ShootingMode.Aesthetic => "Aesthetic",
        ShootingMode.Product   => "Product",
        ShootingMode.Portrait  => "Portrait",
        ShootingMode.Food      => "Food",
        _                      => mode.ToString()
    };

    // Using Unicode characters that roughly correspond to SF Symbols concepts
    public static string IconGlyph(this ShootingMode mode) => mode switch
    {
        ShootingMode.Aesthetic => "✦",
        ShootingMode.Product   => "■",
        ShootingMode.Portrait  => "◉",
        ShootingMode.Food      => "◈",
        _                      => "●"
    };
}
