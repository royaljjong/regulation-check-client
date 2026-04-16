namespace AutomationRawCheck.Application.UseProfiles;

/// <summary>
/// Resolves external selected-use strings into internal generalized use profiles.
/// </summary>
public static class UseProfileResolver
{
    public static UseProfileDefinition Resolve(string selectedUse)
    {
        if (!UseProfileRegistry.TryGet(selectedUse, out var profile))
            throw new InvalidOperationException($"Unsupported selectedUse: {selectedUse}");

        return profile;
    }
}
