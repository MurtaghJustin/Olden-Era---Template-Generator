using System.Reflection;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{

/* Defines logical types of content rules. */
public enum ContentRuleType
{
    DistanceToRoad,
    DistanceToTown,
    Guarded,
    Variant
}
/* Represents an abstract content rule from the content rule management in the UI.
 * Such a rule needs to be properly parsed later for reflecting proper json values in content items. */
public sealed class ContentRule
{
    public ContentRuleType Type { get; }
    public string Name { get; set; }

    /* Abstract handler of specific rule values.
    * @note perhaps more polymorphic approach would be better if this grows too much. */
    public abstract record RuleValue;
    public sealed record DistanceValue(DistanceVariation distanceVariation) : RuleValue;
    public sealed record GuardedValue(bool IsGuarded) : RuleValue;
    public sealed record VariantValue(int VariantId) : RuleValue;
    /* Value should be of the corresponding type based on the rule type. */
    public RuleValue? Value { get; set; }
    public ContentRule(string name, ContentRuleType type, RuleValue? value = null)
    {
        Name = name;
        Type = type;
        Value = value;
    }
    /* Copy constructor for common rule pattern handling */
    public ContentRule(ContentRule other, RuleValue? newValue = null)
    {
        Name = other.Name;
        Type = other.Type;
        Value = newValue ?? other.Value;
    }

    /* Bind-friendly projection for UI controls that cannot call methods directly. */
    public string DisplayName => GetDisplayText();

    /* Get a user-friendly display text for the rule */
    public string GetDisplayText() => Value switch
    {
        DistanceValue d => $"{Name}: {d.distanceVariation.Name}",
        GuardedValue g => $"Guarded: {g.IsGuarded}",
        VariantValue v => $"Variant: {v.VariantId}",
        _ => "Unsupported rule type"
    };
}
/* Content rule manager for processing rules from the UI to the underlying data model.
* Not every "rule" coming from the UI will be a rule type of content items in final templates,
* but all could be handled logically as rules within the system. */
public static class ContentRuleManager
{
    public static ContentRule _ruleDistanceToRoad = new ContentRule("Distance to road", ContentRuleType.DistanceToRoad);
    public static ContentRule _ruleDistanceToTown = new ContentRule("Distance to town", ContentRuleType.DistanceToTown);
    public static ContentRule _ruleGuarded = new ContentRule("Guarded", ContentRuleType.Guarded);
    public static ContentRule _ruleVariant = new ContentRule("Variant", ContentRuleType.Variant);
    
    /* Retrieve all defined Content Rules */
    public static ContentRule[] GetRules()
    {
        return typeof(ContentRuleManager)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ContentRule))
            .Select(field => (ContentRule?)field.GetValue(null))
            .Where(rule => rule is not null)
            .Cast<ContentRule>()
            .ToArray();
    }

    public static ContentRule CreateContentRuleFromPreset(ContentRule preset, object value)
    {
        return new ContentRule(preset, value switch
        {
            /* Distance rules share the same type of value. We still need to differentiate them by type for later parsing. */
            DistanceVariation dv when 
                (preset.Type == ContentRuleType.DistanceToRoad || preset.Type == ContentRuleType.DistanceToTown) 
                    => new ContentRule.DistanceValue(dv),
            bool isGuarded when preset.Type == ContentRuleType.Guarded => new ContentRule.GuardedValue(isGuarded),
            int variantId when preset.Type == ContentRuleType.Variant => new ContentRule.VariantValue(variantId),
            _ => throw new ArgumentException("Unsupported value type for given content rule") // We never should reach this state.
        });
    }

    /* Apply the rules from the UI data storage to the final JSON content item. */
    public static void ApplyRulesToFinalContentItem(ContentItem contentItem, ZoneContentItemUI itemUIData)
    {
        List<ContentPlacementRule> ContentPlacementRules = new List<ContentPlacementRule>();
        bool? isGuarded = null;
        foreach(ContentRule Rule in itemUIData.Rules)
        {
            switch(Rule)
            {
                case {Type: ContentRuleType.DistanceToRoad, Value: ContentRule.DistanceValue dv}:
                    ContentPlacementRules.Add(RulePresets.RoadDistance(dv.distanceVariation));
                    break;
                case {Type: ContentRuleType.DistanceToTown, Value: ContentRule.DistanceValue dv}:
                    ContentPlacementRules.Add(RulePresets.TownDistance(dv.distanceVariation));
                    break;
                case {Type: ContentRuleType.Guarded, Value: ContentRule.GuardedValue guarded}:
                    isGuarded = guarded.IsGuarded;
                    break;
                default:
                    // We never should reach this state. (assuming the UI only allows valid rules to be added).
                    continue;
            }
        }
        /* isGuarded can be null and that's fine - if rule is not set, do not force it in the final ContentItem. */
        contentItem.IsGuarded = isGuarded;
        if (ContentPlacementRules.Count > 0)
        {
            contentItem.Rules ??= new List<ContentPlacementRule>();
            contentItem.Rules.AddRange(ContentPlacementRules);
        }
    }
}

}