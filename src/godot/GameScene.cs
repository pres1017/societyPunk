using Godot;
using System;
using System.Collections.Generic;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;
using SimTileMap = SocietyPunk.Simulation.World.TileMap;

/// <summary>
/// Main game scene. Initializes the simulation world, creates visual nodes
/// for all entities, and wires the simulation clock.
/// No game logic — delegates everything to the simulation layer.
/// </summary>
public partial class GameScene : Node2D
{
    [Export] public int MapWidth { get; set; } = 50;
    [Export] public int MapHeight { get; set; } = 50;
    [Export] public int TileSize { get; set; } = 32;

    private WorldState _state = null!;
    private SimulationRunner _runner = null!;

    private SimulationClockNode _clock = null!;
    private TileMapNode _tileMapNode = null!;
    private Node2D _buildingsLayer = null!;
    private Node2D _popsLayer = null!;
    private Label _hud = null!;

    private readonly Dictionary<Guid, BuildingNode> _buildingNodes = new();
    private readonly Dictionary<Guid, PopNode> _popNodes = new();

    // Camera
    private Camera2D _camera = null!;
    private const float CameraPanSpeed = 400f;
    private const float ZoomStep = 0.1f;
    private const float MinZoom = 0.5f;  // zoomed in (tiles appear 2× size)
    private const float MaxZoom = 3.0f;  // zoomed out (tiles appear 1/3 size)
    private bool _isDragging;
    private Vector2 _dragStart;

    public override void _Ready()
    {
        // Load data
        var dataDir = ProjectSettings.GlobalizePath("res://data");
        var data = GameData.LoadFromDirectory(dataDir);

        // Create world
        var map = new SimTileMap(MapWidth, MapHeight);
        _state = new WorldState
        {
            Map = map,
            Data = data,
        };
        _runner = new SimulationRunner(_state);

        // Create scene tree nodes
        _tileMapNode = new TileMapNode { TileSize = TileSize, SimTileMap = map };
        AddChild(_tileMapNode);

        _buildingsLayer = new Node2D { Name = "Buildings" };
        AddChild(_buildingsLayer);

        _popsLayer = new Node2D { Name = "Pops" };
        AddChild(_popsLayer);

        _clock = new SimulationClockNode();
        _clock.State = _state;
        _clock.Runner = _runner;
        _clock.TickCompleted += OnTickCompleted;
        AddChild(_clock);

        // HUD overlay
        _hud = new Label
        {
            Name = "HUD",
            Position = new Vector2(10, 10),
            Text = "Tick: 0 | Speed: 1 | Paused: press Space"
        };
        var canvas = new CanvasLayer { Name = "UILayer" };
        canvas.AddChild(_hud);
        AddChild(canvas);

        _clock.SpeedChanged += OnSpeedChanged;

        // Camera
        _camera = new Camera2D
        {
            Name = "Camera",
            Enabled = true,
            // Center on the demo area
            Position = new Vector2(20 * TileSize, 15 * TileSize),
        };
        AddChild(_camera);

        // Populate demo scenario
        SetupDemoWorld(map, data);

        // Initial sync and draw
        SyncBuildingNodes();
        SyncPopNodes();
        _tileMapNode.Refresh();
        UpdateHud();
    }

    private void SetupDemoWorld(SimTileMap map, GameData data)
    {
        // Build a cobblestone road network across the map
        for (int x = 2; x < 45; x++)
            map.PlaceRoad(x, 10, RoadType.Cobblestone);
        for (int x = 2; x < 45; x++)
            map.PlaceRoad(x, 20, RoadType.Cobblestone);
        // Vertical connectors
        for (int y = 10; y <= 20; y++)
        {
            map.PlaceRoad(2, y, RoadType.Cobblestone);
            map.PlaceRoad(15, y, RoadType.Cobblestone);
            map.PlaceRoad(28, y, RoadType.Cobblestone);
            map.PlaceRoad(40, y, RoadType.Cobblestone);
        }

        // ═══ Food chain: grain_farm → windmill → bakery ═══
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 2, TileY = 10,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "grain_farming"
        };
        farm.InputBuffer["tools"] = 20f;
        var farmer1 = MakeWorker(ProfessionType.Farmer, 2, 10);
        var farmer2 = MakeWorker(ProfessionType.Farmer, 2, 10);
        var farmer3 = MakeWorker(ProfessionType.Farmer, 2, 10);
        var farmer4 = MakeWorker(ProfessionType.Farmer, 2, 10);
        farm.AssignedWorkerIds.AddRange(new[] { farmer1.Id, farmer2.Id, farmer3.Id, farmer4.Id });

        var mill = new Building
        {
            DefId = "windmill", TileX = 15, TileY = 10,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "milling"
        };
        var miller = MakeWorker(ProfessionType.Miller, 15, 10);
        mill.AssignedWorkerIds.Add(miller.Id);

        var bakery = new Building
        {
            DefId = "bakery", TileX = 28, TileY = 10,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "baking"
        };
        var baker1 = MakeWorker(ProfessionType.Baker, 28, 10);
        var baker2 = MakeWorker(ProfessionType.Baker, 28, 10);
        bakery.AssignedWorkerIds.AddRange(new[] { baker1.Id, baker2.Id });

        // ═══ Fuel chain: charcoal_kiln ═══
        var kiln = new Building
        {
            DefId = "charcoal_kiln", TileX = 2, TileY = 20,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "charcoal_burning"
        };
        kiln.InputBuffer["logs"] = 50f;
        var kilnWorker = MakeWorker(ProfessionType.Laborer, 2, 20);
        kiln.AssignedWorkerIds.Add(kilnWorker.Id);

        // ═══ Warehouse ═══
        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 40, TileY = 10,
            IsConstructed = true, IsOperational = true
        };

        // ═══ Construction site: house ═══
        var houseSite = new Building
        {
            DefId = "house", TileX = 15, TileY = 20,
            IsConstructed = false, IsOperational = false
        };

        // Stock warehouse with construction materials
        warehouse.Stockpile["planks"] = 30f;
        warehouse.Stockpile["stone"] = 20f;

        // ═══ Haulers ═══
        var hauler1 = MakeWorker(ProfessionType.Hauler, 5, 10);
        hauler1.State = PopState.Idle;
        var hauler2 = MakeWorker(ProfessionType.Hauler, 20, 10);
        hauler2.State = PopState.Idle;
        var hauler3 = MakeWorker(ProfessionType.Hauler, 35, 10);
        hauler3.State = PopState.Idle;

        // ═══ Builder ═══
        var builder = MakeWorker(ProfessionType.Builder, 40, 10);
        builder.State = PopState.Idle;

        // Add everything to state
        _state.Buildings.AddRange(new[] { farm, mill, bakery, kiln, warehouse, houseSite });
        _state.Pops.AddRange(new[] {
            farmer1, farmer2, farmer3, farmer4,
            miller, baker1, baker2, kilnWorker,
            hauler1, hauler2, hauler3, builder
        });
    }

    private static Pop MakeWorker(ProfessionType profession, int tileX, int tileY)
    {
        return new Pop
        {
            Profession = profession,
            SkillLevel = 1.0f,
            FoodLevel = 1.0f,
            RestLevel = 1.0f,
            State = PopState.Working,
            TileX = tileX,
            TileY = tileY
        };
    }

    public override void _Process(double delta)
    {
        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    dir.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  dir.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  dir.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;

        if (dir != Vector2.Zero)
        {
            // Scale speed by zoom so panning feels consistent
            _camera.Position += dir.Normalized() * CameraPanSpeed * _camera.Zoom.X * (float)delta;
            ClampCamera();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                var z = Mathf.Max(MinZoom, _camera.Zoom.X - ZoomStep);
                _camera.Zoom = new Vector2(z, z);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                var z = Mathf.Min(MaxZoom, _camera.Zoom.X + ZoomStep);
                _camera.Zoom = new Vector2(z, z);
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                _isDragging = mb.Pressed;
                _dragStart = mb.GlobalPosition;
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            // Move camera opposite to drag direction, scaled by zoom
            _camera.Position -= mm.Relative * _camera.Zoom.X;
            ClampCamera();
        }
    }

    private void ClampCamera()
    {
        var max = new Vector2(MapWidth * TileSize, MapHeight * TileSize);
        _camera.Position = new Vector2(
            Mathf.Clamp(_camera.Position.X, 0, max.X),
            Mathf.Clamp(_camera.Position.Y, 0, max.Y)
        );
    }

    private void OnTickCompleted(int currentTick)
    {
        SyncBuildingNodes();
        SyncPopNodes();
        UpdateHud();
    }

    private void OnSpeedChanged(int newSpeed)
    {
        UpdateHud();
    }

    private void SyncBuildingNodes()
    {
        // Add new buildings
        foreach (var building in _state.Buildings)
        {
            if (!_buildingNodes.ContainsKey(building.Id))
            {
                _state.Data.Buildings.TryGetValue(building.DefId, out var def);
                var node = new BuildingNode
                {
                    SimBuilding = building,
                    Def = def,
                    TileSize = TileSize,
                };
                _buildingsLayer.AddChild(node);
                _buildingNodes[building.Id] = node;
            }
            _buildingNodes[building.Id].Refresh();
        }

        // Remove deleted buildings
        var toRemove = new List<Guid>();
        foreach (var kvp in _buildingNodes)
        {
            bool found = false;
            foreach (var b in _state.Buildings)
            {
                if (b.Id == kvp.Key) { found = true; break; }
            }
            if (!found) toRemove.Add(kvp.Key);
        }
        foreach (var id in toRemove)
        {
            _buildingNodes[id].QueueFree();
            _buildingNodes.Remove(id);
        }
    }

    private void SyncPopNodes()
    {
        // Add new pops
        foreach (var pop in _state.Pops)
        {
            if (!_popNodes.ContainsKey(pop.Id))
            {
                var node = new PopNode
                {
                    SimPop = pop,
                    TileSize = TileSize,
                };
                _popsLayer.AddChild(node);
                _popNodes[pop.Id] = node;
            }
            _popNodes[pop.Id].Refresh();
        }

        // Remove deleted pops
        var toRemove = new List<Guid>();
        foreach (var kvp in _popNodes)
        {
            bool found = false;
            foreach (var p in _state.Pops)
            {
                if (p.Id == kvp.Key) { found = true; break; }
            }
            if (!found) toRemove.Add(kvp.Key);
        }
        foreach (var id in toRemove)
        {
            _popNodes[id].QueueFree();
            _popNodes.Remove(id);
        }
    }

    private void UpdateHud()
    {
        string paused = _clock.IsPaused ? " [PAUSED]" : "";
        _hud.Text = $"Tick: {_state.CurrentTick} | Speed: {_clock.SpeedLevel}{paused} | " +
                     $"Buildings: {_state.Buildings.Count} | Pops: {_state.Pops.Count} | " +
                     $"Keys: Space=pause, 1-5=speed, WASD=pan, Scroll=zoom";
    }
}
