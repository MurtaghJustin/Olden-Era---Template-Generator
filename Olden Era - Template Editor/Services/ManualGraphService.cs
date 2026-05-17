using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Olden_Era___Template_Editor.Services
{
    public sealed class ManualGraphValidationResult
    {
        public List<string> Errors { get; } = [];
        public bool IsValidForExport => Errors.Count == 0;
    }

    public static class ManualGraphService
    {
        public static ManualGraphDocument CreateFromTemplate(RmgTemplate template, bool preferAutomaticGuards)
        {
            var document = new ManualGraphDocument { Enabled = true };
            var variant = template.Variants?.FirstOrDefault();
            if (variant == null)
                return document;

            foreach (Zone zone in variant.Zones ?? [])
            {
                ManualGraphZoneType zoneType = InferZoneType(zone);
                document.Zones.Add(new ManualGraphZone
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = string.IsNullOrWhiteSpace(zone.Name) ? SuggestZoneName(document, zoneType) : zone.Name,
                    ZoneType = zoneType,
                    Layout = string.IsNullOrWhiteSpace(zone.Layout) ? DefaultLayoutFor(zoneType) : zone.Layout!,
                    CastleCount = CountCastles(zone, zoneType),
                    Size = zone.Size ?? 1.0,
                    PreviewPositionX = zone.GeneratorPosition?.X,
                    PreviewPositionY = zone.GeneratorPosition?.Y,
                    PreviewRing = zone.GeneratorRing
                });
            }

            var zoneIdsByName = document.Zones.ToDictionary(z => z.Name, z => z.Id, StringComparer.Ordinal);
            foreach (Connection connection in variant.Connections ?? [])
            {
                if (!zoneIdsByName.TryGetValue(connection.From, out string? fromZoneId)) continue;
                if (!zoneIdsByName.TryGetValue(connection.To, out string? toZoneId)) continue;

                document.Connections.Add(new ManualGraphConnection
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = connection.Name,
                    FromZoneId = fromZoneId,
                    ToZoneId = toZoneId,
                    ConnectionType = string.Equals(connection.ConnectionType, "Portal", StringComparison.OrdinalIgnoreCase)
                        ? ManualGraphConnectionType.Portal
                        : ManualGraphConnectionType.Direct,
                    GuardMode = preferAutomaticGuards ? ManualGraphGuardMode.Auto
                        : connection.GuardValue.HasValue ? ManualGraphGuardMode.Absolute : ManualGraphGuardMode.Auto,
                    GuardScale = 1.0,
                    GuardValue = connection.GuardValue,
                    GuardZoneId = zoneIdsByName.TryGetValue(connection.GuardZone ?? string.Empty, out string? guardZoneId)
                        ? guardZoneId
                        : null,
                    GuardEscape = connection.GuardEscape,
                    SimTurnSquad = connection.SimTurnSquad,
                    GuardWeeklyIncrement = connection.GuardWeeklyIncrement,
                    GuardMatchGroup = connection.GuardMatchGroup,
                    Road = connection.Road,
                    GatePlacement = connection.GatePlacement,
                    Length = connection.Length,
                    PortalPlacementRulesFrom = CloneRules(connection.PortalPlacementRulesFrom),
                    PortalPlacementRulesTo = CloneRules(connection.PortalPlacementRulesTo)
                });
            }

            return document;
        }

        public static ManualGraphValidationResult Validate(ManualGraphDocument? document)
        {
            var result = new ManualGraphValidationResult();
            if (document == null || !document.Enabled)
                return result;

            var zonesById = document.Zones
                .Where(z => !string.IsNullOrWhiteSpace(z.Id))
                .ToDictionary(z => z.Id, StringComparer.Ordinal);

            if (document.Zones.Count == 0)
            {
                result.Errors.Add("Graph mode is enabled but there are no zones.");
                return result;
            }

            int hubCount = document.Zones.Count(z => z.ZoneType == ManualGraphZoneType.Hub);
            if (hubCount > 1)
                result.Errors.Add("Only one hub zone is allowed.");

            foreach (IGrouping<string, ManualGraphZone> duplicateNameGroup in document.Zones
                .Where(zone => !string.IsNullOrWhiteSpace(zone.Name))
                .GroupBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                result.Errors.Add($"Zone name \"{duplicateNameGroup.Key}\" is duplicated.");
            }

            foreach (ManualGraphZone zone in document.Zones)
            {
                if (string.IsNullOrWhiteSpace(zone.Name))
                    result.Errors.Add("Every zone must have a name.");
                if (zone.ZoneType == ManualGraphZoneType.Player && zone.CastleCount < 1)
                    result.Errors.Add($"Player zone \"{zone.Name}\" must have at least 1 castle.");
            }

            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (ManualGraphZone zone in document.Zones)
                adjacency[zone.Id] = [];

            foreach (ManualGraphConnection connection in document.Connections)
            {
                if (!zonesById.ContainsKey(connection.FromZoneId))
                    result.Errors.Add($"Connection \"{ConnectionLabel(connection)}\" references a missing source zone.");
                if (!zonesById.ContainsKey(connection.ToZoneId))
                    result.Errors.Add($"Connection \"{ConnectionLabel(connection)}\" references a missing destination zone.");
                if (connection.FromZoneId == connection.ToZoneId && !string.IsNullOrWhiteSpace(connection.FromZoneId))
                    result.Errors.Add($"Connection \"{ConnectionLabel(connection)}\" cannot connect a zone to itself.");

                if (zonesById.ContainsKey(connection.FromZoneId) && zonesById.ContainsKey(connection.ToZoneId)
                    && connection.FromZoneId != connection.ToZoneId)
                {
                    adjacency[connection.FromZoneId].Add(connection.ToZoneId);
                    adjacency[connection.ToZoneId].Add(connection.FromZoneId);
                }
            }

            foreach (ManualGraphZone zone in document.Zones)
            {
                if (!adjacency.TryGetValue(zone.Id, out var neighbors) || neighbors.Count == 0)
                    result.Errors.Add($"Zone \"{zone.Name}\" must have at least one connection.");
            }

            ManualGraphZone? hub = document.Zones.FirstOrDefault(z => z.ZoneType == ManualGraphZoneType.Hub);
            if (hub != null)
            {
                foreach (ManualGraphZone playerZone in document.Zones.Where(z => z.ZoneType == ManualGraphZoneType.Player))
                {
                    if (!HasPath(adjacency, playerZone.Id, hub.Id))
                        result.Errors.Add($"Player zone \"{playerZone.Name}\" must be able to reach the hub.");
                }
            }

            return result;
        }

        public static int CountPlayerZones(ManualGraphDocument? document) =>
            document?.Zones.Count(z => z.ZoneType == ManualGraphZoneType.Player) ?? 0;

        public static int CountNeutralZones(ManualGraphDocument? document) =>
            document?.Zones.Count(z => z.ZoneType is ManualGraphZoneType.NeutralLow
                or ManualGraphZoneType.NeutralMedium
                or ManualGraphZoneType.NeutralHigh) ?? 0;

        public static bool ApplySharedZoneDefaults(
            ManualGraphDocument? document,
            int playerCastleCount,
            int neutralCastleCount,
            int hubCastleCount,
            double playerZoneSize,
            double neutralZoneSize,
            double hubZoneSize)
        {
            if (document == null)
                return false;

            bool changed = false;
            foreach (ManualGraphZone zone in document.Zones)
            {
                changed |= ApplySharedZoneDefaults(
                    zone,
                    playerCastleCount,
                    neutralCastleCount,
                    hubCastleCount,
                    playerZoneSize,
                    neutralZoneSize,
                    hubZoneSize);
            }

            return changed;
        }

        public static bool ApplySharedZoneDefaults(
            ManualGraphZone zone,
            int playerCastleCount,
            int neutralCastleCount,
            int hubCastleCount,
            double playerZoneSize,
            double neutralZoneSize,
            double hubZoneSize)
        {
            int desiredCastles = zone.CastleCount;
            double desiredSize = zone.Size;

            switch (zone.ZoneType)
            {
                case ManualGraphZoneType.Player:
                    desiredCastles = Math.Clamp(playerCastleCount, 1, 5);
                    desiredSize = Math.Clamp(playerZoneSize, 0.25, 3.0);
                    break;

                case ManualGraphZoneType.Hub:
                    desiredCastles = Math.Clamp(hubCastleCount, 0, 5);
                    desiredSize = Math.Clamp(hubZoneSize, 0.25, 3.0);
                    break;

                case ManualGraphZoneType.NeutralLow:
                case ManualGraphZoneType.NeutralMedium:
                case ManualGraphZoneType.NeutralHigh:
                    desiredCastles = zone.CastleCount > 0
                        ? Math.Clamp(neutralCastleCount, 0, 5)
                        : 0;
                    desiredSize = Math.Clamp(neutralZoneSize, 0.25, 3.0);
                    break;
            }

            bool changed = false;
            if (zone.CastleCount != desiredCastles)
            {
                zone.CastleCount = desiredCastles;
                changed = true;
            }

            if (Math.Abs(zone.Size - desiredSize) > 0.0001)
            {
                zone.Size = desiredSize;
                changed = true;
            }

            return changed;
        }

        public static string SuggestZoneName(ManualGraphDocument document, ManualGraphZoneType zoneType)
        {
            string prefix = zoneType switch
            {
                ManualGraphZoneType.Player => "Player Zone",
                ManualGraphZoneType.NeutralLow => "Low Neutral",
                ManualGraphZoneType.NeutralMedium => "Medium Neutral",
                ManualGraphZoneType.NeutralHigh => "High Neutral",
                ManualGraphZoneType.Hub => "Hub",
                _ => "Zone"
            };

            int suffix = 1;
            string candidate = prefix;
            while (document.Zones.Any(z => string.Equals(z.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                suffix++;
                candidate = $"{prefix} {suffix}";
            }

            return candidate;
        }

        public static string DefaultLayoutFor(ManualGraphZoneType zoneType) => zoneType switch
        {
            ManualGraphZoneType.Player => "zone_layout_spawns",
            ManualGraphZoneType.NeutralLow => "zone_layout_sides",
            ManualGraphZoneType.NeutralMedium => "zone_layout_treasure_zone",
            ManualGraphZoneType.NeutralHigh => "zone_layout_treasure_zone",
            ManualGraphZoneType.Hub => "zone_layout_center",
            _ => "zone_layout_spawns"
        };

        public static void EnsurePreviewHints(ManualGraphDocument? document)
        {
            if (document == null)
                return;

            foreach (IGrouping<ManualGraphZoneType, ManualGraphZone> group in document.Zones
                .GroupBy(zone => zone.ZoneType))
            {
                List<ManualGraphZone> zones = group
                    .OrderBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (int i = 0; i < zones.Count; i++)
                {
                    ManualGraphZone zone = zones[i];
                    zone.PreviewRing ??= DefaultPreviewRingFor(zone.ZoneType);

                    if (zone.PreviewPositionX.HasValue && zone.PreviewPositionY.HasValue)
                        continue;

                    (double x, double y) = DefaultPreviewPositionFor(zone.ZoneType, i, zones.Count);
                    zone.PreviewPositionX = x;
                    zone.PreviewPositionY = y;
                }
            }
        }

        public static int DefaultPreviewRingFor(ManualGraphZoneType zoneType) => zoneType switch
        {
            ManualGraphZoneType.Player => 0,
            ManualGraphZoneType.NeutralLow => 1,
            ManualGraphZoneType.NeutralMedium => 2,
            ManualGraphZoneType.NeutralHigh => 3,
            ManualGraphZoneType.Hub => 4,
            _ => 2
        };

        private static (double X, double Y) DefaultPreviewPositionFor(ManualGraphZoneType zoneType, int index, int count)
        {
            if (zoneType == ManualGraphZoneType.Hub)
                return (0.5, 0.5);

            double radius = zoneType switch
            {
                ManualGraphZoneType.Player => 0.42,
                ManualGraphZoneType.NeutralLow => 0.33,
                ManualGraphZoneType.NeutralMedium => 0.24,
                ManualGraphZoneType.NeutralHigh => 0.16,
                _ => 0.28
            };

            double angle = -Math.PI / 2.0;
            if (count > 1)
                angle += index * Math.PI * 2.0 / count;

            return
            (
                0.5 + Math.Cos(angle) * radius,
                0.5 + Math.Sin(angle) * radius
            );
        }

        private static ManualGraphZoneType InferZoneType(Zone zone)
        {
            if (string.Equals(zone.EditorZoneType, nameof(ManualGraphZoneType.Player), StringComparison.Ordinal))
                return ManualGraphZoneType.Player;
            if (string.Equals(zone.EditorZoneType, nameof(ManualGraphZoneType.NeutralLow), StringComparison.Ordinal))
                return ManualGraphZoneType.NeutralLow;
            if (string.Equals(zone.EditorZoneType, nameof(ManualGraphZoneType.NeutralMedium), StringComparison.Ordinal))
                return ManualGraphZoneType.NeutralMedium;
            if (string.Equals(zone.EditorZoneType, nameof(ManualGraphZoneType.NeutralHigh), StringComparison.Ordinal))
                return ManualGraphZoneType.NeutralHigh;
            if (string.Equals(zone.EditorZoneType, nameof(ManualGraphZoneType.Hub), StringComparison.Ordinal))
                return ManualGraphZoneType.Hub;

            if (zone.MainObjects?.Any(o => o.Type == "Spawn") == true)
                return ManualGraphZoneType.Player;
            if (string.Equals(zone.Name, "Hub", StringComparison.OrdinalIgnoreCase)
                || zone.Name.StartsWith("Hub-", StringComparison.OrdinalIgnoreCase))
                return ManualGraphZoneType.Hub;

            string joinedPools = string.Join(" ", zone.GuardedContentPool ?? []);
            if (joinedPools.Contains("_t5_", StringComparison.OrdinalIgnoreCase)
                || joinedPools.Contains("_t4_", StringComparison.OrdinalIgnoreCase))
                return ManualGraphZoneType.NeutralHigh;
            if (joinedPools.Contains("_t2_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(zone.Layout, "zone_layout_sides", StringComparison.OrdinalIgnoreCase))
                return ManualGraphZoneType.NeutralLow;
            return ManualGraphZoneType.NeutralMedium;
        }

        private static int CountCastles(Zone zone, ManualGraphZoneType zoneType)
        {
            int cityCount = 0;
            foreach (MainObject mainObject in zone.MainObjects ?? [])
            {
                if (mainObject.Type is "Spawn" or "City")
                    cityCount++;
            }

            if (zoneType == ManualGraphZoneType.Player)
                return Math.Max(1, cityCount);
            return Math.Clamp(cityCount, 0, 5);
        }

        private static string ConnectionLabel(ManualGraphConnection connection) =>
            !string.IsNullOrWhiteSpace(connection.Name) ? connection.Name! : connection.Id;

        private static bool HasPath(Dictionary<string, HashSet<string>> adjacency, string startId, string endId)
        {
            if (startId == endId)
                return true;

            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { startId };
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out var neighbors))
                    continue;

                foreach (string neighbor in neighbors)
                {
                    if (!visited.Add(neighbor))
                        continue;
                    if (neighbor == endId)
                        return true;
                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        private static List<ContentPlacementRule>? CloneRules(List<ContentPlacementRule>? rules)
        {
            if (rules == null)
                return null;

            return rules.Select(rule => new ContentPlacementRule
            {
                Type = rule.Type,
                Args = rule.Args != null ? [.. rule.Args] : null,
                TargetMin = rule.TargetMin,
                TargetMax = rule.TargetMax,
                Weight = rule.Weight
            }).ToList();
        }
    }
}
