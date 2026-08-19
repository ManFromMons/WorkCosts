namespace WorkCosts.Helpers;

public static class ProductEquivalentHelper
{
    public static (Guid ProductId, Guid EquivalentProductId) OrderPair(Guid left, Guid right) =>
        left.CompareTo(right) < 0 ? (left, right) : (right, left);
}
