using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using Xunit;

namespace Olden_Era___Template_Editor.Tests;

public class ManualGraphServiceTests
{
    [Fact]
    public void Validate_FlagsDisconnectedHubAndUnconnectedZones()
    {
        var graph = new ManualGraphDocument
        {
            Enabled = true,
            Zones =
            [
                new ManualGraphZone { Id = "p1", Name = "Player One", ZoneType = ManualGraphZoneType.Player, Layout = "zone_layout_spawns", CastleCount = 1, Size = 1.0 },
                new ManualGraphZone { Id = "hub", Name = "Center Hub", ZoneType = ManualGraphZoneType.Hub, Layout = "zone_layout_center", CastleCount = 0, Size = 1.0 },
                new ManualGraphZone { Id = "n1", Name = "Outer Neutral", ZoneType = ManualGraphZoneType.NeutralMedium, Layout = "zone_layout_treasure_zone", CastleCount = 0, Size = 1.0 }
            ],
            Connections = []
        };

        ManualGraphValidationResult result = ManualGraphService.Validate(graph);

        Assert.False(result.IsValidForExport);
        Assert.Contains(result.Errors, error => error.Contains("must have at least one connection", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("reach the hub", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_ManualGraphPreservesCustomZoneNamesAndConnectionTypes()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Graph Template",
            MapSize = 160,
            ZoneCfg = new ZoneConfiguration
            {
                ResourceDensityPercent = 100,
                StructureDensityPercent = 100,
                NeutralStackStrengthPercent = 100,
                BorderGuardStrengthPercent = 100
            },
            ManualGraph = new ManualGraphDocument
            {
                Enabled = true,
                Zones =
                [
                    new ManualGraphZone { Id = "p1", Name = "North Start", ZoneType = ManualGraphZoneType.Player, Layout = "zone_layout_spawns", CastleCount = 1, Size = 1.0 },
                    new ManualGraphZone { Id = "hub", Name = "Transit Hub", ZoneType = ManualGraphZoneType.Hub, Layout = "zone_layout_center", CastleCount = 0, Size = 1.0 },
                    new ManualGraphZone { Id = "n1", Name = "Treasure Pocket", ZoneType = ManualGraphZoneType.NeutralHigh, Layout = "zone_layout_treasure_zone", CastleCount = 0, Size = 1.0 }
                ],
                Connections =
                [
                    new ManualGraphConnection
                    {
                        Id = "c1",
                        Name = "Start-To-Hub",
                        FromZoneId = "p1",
                        ToZoneId = "hub",
                        ConnectionType = ManualGraphConnectionType.Direct,
                        GuardMode = ManualGraphGuardMode.Absolute,
                        GuardValue = 12345
                    },
                    new ManualGraphConnection
                    {
                        Id = "c2",
                        Name = "Hub-To-Treasure",
                        FromZoneId = "hub",
                        ToZoneId = "n1",
                        ConnectionType = ManualGraphConnectionType.Portal,
                        GuardMode = ManualGraphGuardMode.Auto
                    }
                ]
            }
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        Variant variant = Assert.Single(template.Variants!);
        Connection direct = Assert.Single(variant.Connections!, connection => connection.Name == "Start-To-Hub");
        Connection portal = Assert.Single(variant.Connections!, connection => connection.Name == "Hub-To-Treasure");

        Assert.Contains(variant.Zones!, zone => zone.Name == "North Start");
        Assert.Contains(variant.Zones!, zone => zone.Name == "Transit Hub");
        Assert.Contains(variant.Zones!, zone => zone.Name == "Treasure Pocket");
        Assert.Equal("Direct", direct.ConnectionType);
        Assert.Equal(12345, direct.GuardValue);
        Assert.Equal("Portal", portal.ConnectionType);
    }

    [Fact]
    public void CreateFromTemplate_PreservesPreviewHints()
    {
        var template = new RmgTemplate
        {
            Variants =
            [
                new Variant
                {
                    Zones =
                    [
                        new Zone
                        {
                            Name = "Spawn-A",
                            Layout = "zone_layout_spawns",
                            GeneratorPosition = (0.25, 0.75),
                            GeneratorRing = 0,
                            MainObjects = [new MainObject { Type = "Spawn" }]
                        }
                    ]
                }
            ]
        };

        ManualGraphDocument document = ManualGraphService.CreateFromTemplate(template, preferAutomaticGuards: true);
        ManualGraphZone zone = Assert.Single(document.Zones);

        Assert.Equal(0.25, zone.PreviewPositionX);
        Assert.Equal(0.75, zone.PreviewPositionY);
        Assert.Equal(0, zone.PreviewRing);
    }

    [Fact]
    public void EnsurePreviewHints_FillsMissingHints()
    {
        var document = new ManualGraphDocument
        {
            Zones =
            [
                new ManualGraphZone
                {
                    Id = "p1",
                    Name = "Spawn-A",
                    ZoneType = ManualGraphZoneType.Player,
                    Layout = "zone_layout_spawns",
                    CastleCount = 1,
                    Size = 1.0
                },
                new ManualGraphZone
                {
                    Id = "hub",
                    Name = "Hub",
                    ZoneType = ManualGraphZoneType.Hub,
                    Layout = "zone_layout_center",
                    CastleCount = 0,
                    Size = 1.0
                }
            ]
        };

        ManualGraphService.EnsurePreviewHints(document);

        Assert.All(document.Zones, zone =>
        {
            Assert.True(zone.PreviewPositionX.HasValue);
            Assert.True(zone.PreviewPositionY.HasValue);
            Assert.True(zone.PreviewRing.HasValue);
        });
        Assert.Equal(0.5, document.Zones[1].PreviewPositionX);
        Assert.Equal(0.5, document.Zones[1].PreviewPositionY);
    }

    [Fact]
    public void ApplySharedZoneDefaults_ResetsZonesToCurrentTypeDefaults()
    {
        var graph = new ManualGraphDocument
        {
            Zones =
            [
                new ManualGraphZone { Id = "p1", Name = "Spawn-A", ZoneType = ManualGraphZoneType.Player, Layout = "zone_layout_spawns", CastleCount = 3, Size = 1.7 },
                new ManualGraphZone { Id = "n1", Name = "Low With Castle", ZoneType = ManualGraphZoneType.NeutralLow, Layout = "zone_layout_sides", CastleCount = 2, Size = 1.8 },
                new ManualGraphZone { Id = "n2", Name = "Low No Castle", ZoneType = ManualGraphZoneType.NeutralLow, Layout = "zone_layout_sides", CastleCount = 0, Size = 1.8 },
                new ManualGraphZone { Id = "hub", Name = "Hub", ZoneType = ManualGraphZoneType.Hub, Layout = "zone_layout_center", CastleCount = 5, Size = 2.5 }
            ]
        };

        bool changed = ManualGraphService.ApplySharedZoneDefaults(
            graph,
            playerCastleCount: 1,
            neutralCastleCount: 4,
            hubCastleCount: 2,
            playerZoneSize: 1.1,
            neutralZoneSize: 0.8,
            hubZoneSize: 1.4);

        Assert.True(changed);
        Assert.Equal(1, graph.Zones[0].CastleCount);
        Assert.Equal(1.1, graph.Zones[0].Size);
        Assert.Equal(4, graph.Zones[1].CastleCount);
        Assert.Equal(0.8, graph.Zones[1].Size);
        Assert.Equal(0, graph.Zones[2].CastleCount);
        Assert.Equal(0.8, graph.Zones[2].Size);
        Assert.Equal(2, graph.Zones[3].CastleCount);
        Assert.Equal(1.4, graph.Zones[3].Size);
    }
}
