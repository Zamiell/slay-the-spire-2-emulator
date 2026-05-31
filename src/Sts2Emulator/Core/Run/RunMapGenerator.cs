using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public static class RunMapGenerator
{
    public static void SelectActAndGenerateRooms(RunState state)
    {
        var actRng = new DotNetRandom(unchecked((int)state.Rng.Seed));
        bool underdocks = actRng.NextBool();
        state.Act = underdocks ? RunConstants.ActUnderdocks : RunConstants.ActOvergrowth;
        state.EventSequence = GenerateEventSequence(state, underdocks);
        state.EventSequenceIndex = 0;

        var upFront = state.Rng.UpFront;
        for (int i = 0; i < 202; i++)
            upFront.NextDouble();
        for (int i = 0; i < (underdocks ? 57 : 60); i++)
            upFront.NextInt((underdocks ? 57 : 60) + 1);

        int[] weakPool = (
            underdocks
                ? RunConstants.UnderdocksWeakEncounters
                : RunConstants.OvergrowthWeakEncounters
        ).ToArray();
        int[] normalPool = (
            underdocks
                ? RunConstants.UnderdocksNormalEncounters
                : RunConstants.OvergrowthNormalEncounters
        ).ToArray();
        int[] elitePool = (
            underdocks
                ? RunConstants.UnderdocksEliteEncounters
                : RunConstants.OvergrowthEliteEncounters
        ).ToArray();
        int[] bossPool = (
            underdocks
                ? RunConstants.UnderdocksBossEncounters
                : RunConstants.OvergrowthBossEncounters
        ).ToArray();

        var normalSequence = new List<int>();
        var weakBag = weakPool.ToList();
        int? last = null;
        for (int i = 0; i < 3; i++)
        {
            int enc = GrabWithoutRepeatingTags(weakBag, last, upFront);
            normalSequence.Add(enc);
            last = enc;
        }

        var normalBag = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            if (normalBag.Count == 0)
                normalBag = normalPool.ToList();
            int enc = GrabWithoutRepeatingTags(normalBag, last, upFront);
            normalSequence.Add(enc);
            last = enc;
        }
        state.NormalEncounterSequence = normalSequence.ToArray();

        var eliteSequence = new List<int>();
        var eliteBag = new List<int>();
        for (int i = 0; i < 15; i++)
        {
            if (eliteBag.Count == 0)
                eliteBag = elitePool.ToList();
            int index = (int)(upFront.NextDouble() * eliteBag.Count);
            eliteSequence.Add(eliteBag[index]);
            eliteBag.RemoveAt(index);
        }
        state.EliteEncounterSequence = eliteSequence.ToArray();
        state.BossEncounterId = bossPool[(int)(upFront.NextDouble() * bossPool.Length)];
    }

    private static int[] GenerateEventSequence(RunState state, bool underdocks)
    {
        int[] eventPool = underdocks
            ?
            [
                0, // Abyssal Baths
                0, // Drowning Beacon
                0, // Endless Conveyor
                0, // Punch-Off
                0, // Spiraling Whirlpool
                0, // Sunken Statue
                RunConstants.EventSunkenTreasury,
                RunConstants.EventDoorsOfLightAndDark,
                0, // Trash Heap
                0, // Waterlogged Scriptorium
            ]
            :
            [
                RunConstants.EventAromaOfChaos,
                0, // Byrdonis Nest
                0, // Dense Vegetation
                RunConstants.EventJungleMazeAdventure,
                0, // Luminous Choir
                RunConstants.EventMorphicGrove,
                0, // Sapphire Seed
                0, // Sunken Statue
                0, // Tablet of Truth
                RunConstants.EventUnrestSite,
                0, // Wellspring
                0, // Whispering Hollow
                0, // Wood Carvings
            ];

        var rng = new GameRng(state.Rng.Seed, "up_front");
        rng.Shuffle(eventPool);
        return eventPool.Where(eventId => eventId != 0).ToArray();
    }

    public static void GenerateActMap(RunState state)
    {
        state.MapNodes = [];
        state.CurrentMapCoord = (RunConstants.MapStartCol, 0);
        Array.Clear(state.MapOptionCoords);
        GetOrCreate(state, RunConstants.MapStartCol, 0).NodeType = RunConstants.NodeNone;
        GetOrCreate(state, RunConstants.MapStartCol, RunConstants.MapBossRow).NodeType =
            RunConstants.NodeBoss;

        var mapRng = state.Rng.ActMapRng(0);
        int restCount = mapRng.NextGaussianInt(7, 1, 6, 7);
        int unknownCount = mapRng.NextGaussianInt(12, 1, 10, 14);

        var starts = new List<(int Col, int Row)>();
        for (int path = 0; path < RunConstants.MapPathIterations; path++)
        {
            int startCol = mapRng.NextInt(RunConstants.MapWidth);
            if (path == 1)
                while (starts.Contains((startCol, 1)))
                    startCol = mapRng.NextInt(RunConstants.MapWidth);
            var current = GetOrCreate(state, startCol, 1);
            if (!starts.Contains((startCol, 1)))
                starts.Add((startCol, 1));
            GeneratePath(state, mapRng, current);
        }

        foreach (var start in starts)
            AddEdge(state, state.CurrentMapCoord, start);
        foreach (
            var node in state
                .MapNodes.Values.Where(n => n.Row == RunConstants.MapBossRow - 1)
                .ToArray()
        )
            AddEdge(
                state,
                (node.Col, node.Row),
                (RunConstants.MapStartCol, RunConstants.MapBossRow)
            );

        AssignPointTypes(state, mapRng, restCount, unknownCount);
        AssignEncounterIds(state);
        RefreshMapOptions(state);
    }

    public static void RefreshMapOptions(RunState state)
    {
        Array.Clear(state.MapNodeTypes);
        Array.Clear(state.MapChoices);
        Array.Clear(state.MapOptionCoords);
        if (!state.MapNodes.TryGetValue(state.CurrentMapCoord, out var current))
            return;

        var options = current
            .Children.OrderBy(coord => coord.Row)
            .ThenBy(coord => coord.Col)
            .Take(RunConstants.MapChoices)
            .ToArray();
        for (int i = 0; i < options.Length; i++)
        {
            var node = state.MapNodes[options[i]];
            state.MapNodeTypes[i] = node.NodeType;
            state.MapChoices[i] = node.NodeType switch
            {
                RunConstants.NodeNormal => state.NormalEncounterSequence[
                    state.NormalEncountersVisited % state.NormalEncounterSequence.Length
                ],
                RunConstants.NodeElite => state.EliteEncounterSequence[
                    state.EliteEncountersVisited % state.EliteEncounterSequence.Length
                ],
                RunConstants.NodeBoss => state.BossEncounterId,
                _ => node.EncounterId,
            };
            state.MapOptionCoords[i] = options[i];
        }
    }

    public static bool ChooseMapNode(
        RunState state,
        int action,
        out int nodeType,
        out int encounterId
    )
    {
        nodeType = RunConstants.NodeNone;
        encounterId = 0;
        if (
            (uint)action >= RunConstants.MapChoices
            || state.MapNodeTypes[action] == RunConstants.NodeNone
        )
            return false;
        var coord = state.MapOptionCoords[action];
        if (coord is null)
            return false;

        nodeType = state.MapNodeTypes[action];
        encounterId = state.MapChoices[action];
        state.CurrentMapCoord = coord.Value;
        state.CurrentNodeType = nodeType;
        state.Floor++;
        if (nodeType == RunConstants.NodeNormal)
            state.NormalEncountersVisited++;
        else if (nodeType == RunConstants.NodeElite)
            state.EliteEncountersVisited++;

        Array.Clear(state.MapNodeTypes);
        Array.Clear(state.MapChoices);
        Array.Clear(state.MapOptionCoords);
        return true;
    }

    private static void GeneratePath(RunState state, GameRng rng, RunMapNode start)
    {
        var current = start;
        while (current.Row < RunConstants.MapBossRow - 1)
        {
            var child = GenerateNextCoord(state, rng, current);
            AddEdge(state, (current.Col, current.Row), child);
            current = state.MapNodes[child];
        }
    }

    private static (int Col, int Row) GenerateNextCoord(
        RunState state,
        GameRng rng,
        RunMapNode current
    )
    {
        var deltas = new List<int> { -1, 0, 1 };
        rng.StableShuffle(deltas, Comparer<int>.Default);
        foreach (int delta in deltas)
        {
            int target = Math.Clamp(current.Col + delta, 0, RunConstants.MapWidth - 1);
            if (!HasInvalidCrossover(state, current, target))
                return (target, current.Row + 1);
        }
        return (current.Col, current.Row + 1);
    }

    private static bool HasInvalidCrossover(RunState state, RunMapNode current, int targetCol)
    {
        int delta = targetCol - current.Col;
        if (delta == 0 || !state.MapNodes.TryGetValue((targetCol, current.Row), out var sibling))
            return false;
        return sibling.Children.Any(child => child.Col - sibling.Col == -delta);
    }

    private static void AssignPointTypes(
        RunState state,
        GameRng rng,
        int restCount,
        int unknownCount
    )
    {
        foreach (var node in state.MapNodes.Values)
        {
            node.NodeType = node.Row switch
            {
                0 => RunConstants.NodeNone,
                1 => RunConstants.NodeNormal,
                RunConstants.MapTreasureRow => RunConstants.NodeRelic,
                RunConstants.MapFinalRestRow => RunConstants.NodeRest,
                RunConstants.MapBossRow => RunConstants.NodeBoss,
                _ => RunConstants.NodeNone,
            };
        }

        var pointTypes = new Queue<int>(
            Enumerable
                .Repeat(RunConstants.NodeRest, restCount)
                .Concat(Enumerable.Repeat(RunConstants.NodeShop, 3))
                .Concat(Enumerable.Repeat(RunConstants.NodeElite, 8))
                .Concat(Enumerable.Repeat(RunConstants.NodeEvent, unknownCount))
        );
        for (int pass = 0; pass < 3 && pointTypes.Count > 0; pass++)
        {
            var candidates = state
                .MapNodes.Values.Where(n =>
                    n.NodeType == RunConstants.NodeNone
                    && n.Row is > 1 and < RunConstants.MapFinalRestRow
                    && n.Row != RunConstants.MapTreasureRow
                )
                .ToList();
            rng.StableShuffle(candidates, CompareNodesByColThenRow);
            foreach (var node in candidates)
            {
                if (pointTypes.Count == 0)
                    break;
                node.NodeType = GetNextValidPointType(state, pointTypes, node);
            }
        }

        foreach (
            var node in state.MapNodes.Values.Where(n =>
                n.NodeType == RunConstants.NodeNone && n.Row > 0
            )
        )
            node.NodeType = RunConstants.NodeNormal;
    }

    private static int GetNextValidPointType(RunState state, Queue<int> pointTypes, RunMapNode node)
    {
        for (int i = 0; i < pointTypes.Count; i++)
        {
            int nodeType = pointTypes.Dequeue();
            if (IsValidPointType(state, nodeType, node))
                return nodeType;
            pointTypes.Enqueue(nodeType);
        }
        return RunConstants.NodeNone;
    }

    private static bool IsValidPointType(RunState state, int nodeType, RunMapNode node) =>
        IsValidForLower(nodeType, node)
        && IsValidForUpper(nodeType, node)
        && IsValidWithParentsAndChildren(state, nodeType, node)
        && IsValidWithChildren(state, nodeType, node)
        && IsValidWithSiblings(state, nodeType, node);

    private static bool IsValidForLower(int nodeType, RunMapNode node) =>
        node.Row >= 6 || nodeType is not (RunConstants.NodeRest or RunConstants.NodeElite);

    private static bool IsValidForUpper(int nodeType, RunMapNode node) =>
        node.Row < RunConstants.MapBossRow - 3 || nodeType != RunConstants.NodeRest;

    private static bool IsValidWithParentsAndChildren(
        RunState state,
        int nodeType,
        RunMapNode node
    ) =>
        !HasParentChildRestriction(nodeType)
        || !node
            .Parents.Concat(node.Children)
            .Any(coord => MapNodeTypeAt(state, coord) == nodeType);

    private static bool IsValidWithChildren(RunState state, int nodeType, RunMapNode node) =>
        !HasParentChildRestriction(nodeType)
        || !node.Children.Any(coord => MapNodeTypeAt(state, coord) == nodeType);

    private static bool IsValidWithSiblings(RunState state, int nodeType, RunMapNode node) =>
        !HasSiblingRestriction(nodeType)
        || !node
            .Parents.SelectMany(parent => MapNodeAt(state, parent).Children)
            .Where(coord => coord != (node.Col, node.Row))
            .Any(coord => MapNodeTypeAt(state, coord) == nodeType);

    private static RunMapNode MapNodeAt(RunState state, (int Col, int Row) coord) =>
        state.MapNodes[coord];

    private static int MapNodeTypeAt(RunState state, (int Col, int Row) coord) =>
        MapNodeAt(state, coord).NodeType;

    private static bool HasParentChildRestriction(int nodeType) =>
        nodeType
            is RunConstants.NodeElite
                or RunConstants.NodeRest
                or RunConstants.NodeRelic
                or RunConstants.NodeShop;

    private static bool HasSiblingRestriction(int nodeType) =>
        nodeType
            is RunConstants.NodeRest
                or RunConstants.NodeNormal
                or RunConstants.NodeEvent
                or RunConstants.NodeElite
                or RunConstants.NodeShop;

    private static readonly Comparer<RunMapNode> CompareNodesByColThenRow =
        Comparer<RunMapNode>.Create(
            (a, b) => a.Col != b.Col ? a.Col.CompareTo(b.Col) : a.Row.CompareTo(b.Row)
        );

    private static void AssignEncounterIds(RunState state)
    {
        foreach (
            var group in state
                .MapNodes.Values.Where(n => n.NodeType == RunConstants.NodeNormal)
                .GroupBy(n => n.Row)
                .OrderBy(g => g.Key)
        )
        {
            int index = Math.Min(group.Key - 1, state.NormalEncounterSequence.Length - 1);
            foreach (var node in group)
                node.EncounterId = state.NormalEncounterSequence[Math.Max(0, index)];
        }
        foreach (
            var group in state
                .MapNodes.Values.Where(n => n.NodeType == RunConstants.NodeElite)
                .GroupBy(n => n.Row)
                .OrderBy(g => g.Key)
        )
        {
            int index = Math.Min(group.Key, state.EliteEncounterSequence.Length - 1);
            foreach (var node in group)
                node.EncounterId = state.EliteEncounterSequence[Math.Max(0, index)];
        }
        foreach (var node in state.MapNodes.Values.Where(n => n.NodeType == RunConstants.NodeBoss))
            node.EncounterId = state.BossEncounterId;
    }

    private static RunMapNode GetOrCreate(RunState state, int col, int row)
    {
        if (!state.MapNodes.TryGetValue((col, row), out var node))
        {
            node = new RunMapNode(col, row);
            state.MapNodes[(col, row)] = node;
        }
        return node;
    }

    private static void AddEdge(
        RunState state,
        (int Col, int Row) parentCoord,
        (int Col, int Row) childCoord
    )
    {
        var parent = GetOrCreate(state, parentCoord.Col, parentCoord.Row);
        var child = GetOrCreate(state, childCoord.Col, childCoord.Row);
        if (!parent.Children.Contains(childCoord))
            parent.Children.Add(childCoord);
        if (!child.Parents.Contains(parentCoord))
            child.Parents.Add(parentCoord);
    }

    private static int GrabWithoutRepeatingTags(List<int> bag, int? lastEncounter, GameRng rng)
    {
        var lastTags = lastEncounter.HasValue ? Tags(lastEncounter.Value) : [];
        bool anyValid = bag.Any(enc => enc != lastEncounter && !Tags(enc).Overlaps(lastTags));
        while (true)
        {
            int index = (int)(rng.NextDouble() * bag.Count);
            int enc = bag[index];
            if (!anyValid || (enc != lastEncounter && !Tags(enc).Overlaps(lastTags)))
            {
                bag.RemoveAt(index);
                return enc;
            }
        }
    }

    private static HashSet<string> Tags(int encounterId) =>
        encounterId switch
        {
            2 => ["Nibbit"],
            3 => ["Slimes"],
            8 => ["Crawler"],
            9 => ["Slugs"],
            11 => ["Shrinker"],
            12 => ["Seapunk"],
            15 => ["Nibbit"],
            16 => ["Slimes"],
            17 => ["Mushroom", "Slimes"],
            18 => ["Mushroom"],
            21 => ["Shrinker", "Crawler"],
            _ => [],
        };
}
