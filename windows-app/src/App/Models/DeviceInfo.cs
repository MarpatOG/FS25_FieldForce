namespace FieldForce.App.Models;

public sealed record DeviceInfo(
    string StableId,
    Guid InstanceGuid,
    Guid ProductGuid,
    string InstanceName,
    string ProductName,
    string DeviceType,
    bool IsForceFeedbackCapable,
    IReadOnlyList<string> Axes,
    IReadOnlyList<string> SupportedEffects)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ProductName) ? InstanceName : ProductName;
    public string AxesText => Axes.Count == 0 ? "No axes reported" : string.Join(", ", Axes);
    public string EffectsText => SupportedEffects.Count == 0 ? "No FFB effects reported" : string.Join(", ", SupportedEffects);
    public string FfbStatus => IsForceFeedbackCapable ? "FFB supported" : "No FFB";
}
