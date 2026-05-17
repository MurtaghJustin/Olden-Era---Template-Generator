using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ManualGraphZoneType
    {
        Player,
        NeutralLow,
        NeutralMedium,
        NeutralHigh,
        Hub
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ManualGraphConnectionType
    {
        Direct,
        Portal
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ManualGraphGuardMode
    {
        Auto,
        Scale,
        Absolute
    }

    public sealed class ManualGraphDocument
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("zones")]
        public List<ManualGraphZone> Zones { get; set; } = [];

        [JsonPropertyName("connections")]
        public List<ManualGraphConnection> Connections { get; set; } = [];
    }

    public sealed class ManualGraphZone
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("zoneType")]
        public ManualGraphZoneType ZoneType { get; set; }

        [JsonPropertyName("layout")]
        public string Layout { get; set; } = string.Empty;

        [JsonPropertyName("castleCount")]
        public int CastleCount { get; set; }

        [JsonPropertyName("size")]
        public double Size { get; set; } = 1.0;

        [JsonPropertyName("previewPositionX")]
        public double? PreviewPositionX { get; set; }

        [JsonPropertyName("previewPositionY")]
        public double? PreviewPositionY { get; set; }

        [JsonPropertyName("previewRing")]
        public int? PreviewRing { get; set; }
    }

    public sealed class ManualGraphConnection
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("fromZoneId")]
        public string FromZoneId { get; set; } = string.Empty;

        [JsonPropertyName("toZoneId")]
        public string ToZoneId { get; set; } = string.Empty;

        [JsonPropertyName("connectionType")]
        public ManualGraphConnectionType ConnectionType { get; set; } = ManualGraphConnectionType.Direct;

        [JsonPropertyName("guardMode")]
        public ManualGraphGuardMode GuardMode { get; set; } = ManualGraphGuardMode.Auto;

        [JsonPropertyName("guardScale")]
        public double GuardScale { get; set; } = 1.0;

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }

        [JsonPropertyName("guardZoneId")]
        public string? GuardZoneId { get; set; }

        [JsonPropertyName("guardEscape")]
        public bool? GuardEscape { get; set; }

        [JsonPropertyName("simTurnSquad")]
        public bool? SimTurnSquad { get; set; }

        [JsonPropertyName("guardWeeklyIncrement")]
        public double? GuardWeeklyIncrement { get; set; }

        [JsonPropertyName("guardMatchGroup")]
        public string? GuardMatchGroup { get; set; }

        [JsonPropertyName("road")]
        public bool? Road { get; set; }

        [JsonPropertyName("gatePlacement")]
        public string? GatePlacement { get; set; }

        [JsonPropertyName("length")]
        public double? Length { get; set; }

        [JsonPropertyName("portalPlacementRulesFrom")]
        public List<ContentPlacementRule>? PortalPlacementRulesFrom { get; set; }

        [JsonPropertyName("portalPlacementRulesTo")]
        public List<ContentPlacementRule>? PortalPlacementRulesTo { get; set; }
    }
}
