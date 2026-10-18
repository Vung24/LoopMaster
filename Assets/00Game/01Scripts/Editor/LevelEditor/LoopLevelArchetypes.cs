#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

public static class LoopLevelArchetypes
{
    public sealed class ArchetypeDef
    {
        public int Id;
        public string Title;
        public string Description;
        public int BoxCount;
        public int ColorCount;
        public bool IsOpenRoad;
        public bool HasSecrets;
        public bool HasLocks;
        public Func<int, JObject> Create;
    }

    private static List<ArchetypeDef> _all;

    public static IReadOnlyList<ArchetypeDef> All
    {
        get
        {
            if (_all == null)
            {
                _all = new List<ArchetypeDef>
                {
                    new ArchetypeDef
                    {
                        Id = 1,
                        Title = "Mẫu 1: Tuyến mở thẳng 2 hộp (Level 1)",
                        Description = "Băng chuyền thẳng có Cổng Vào & Cổng Ra, 2 hộp đối diện (cơ bản mở đầu)",
                        BoxCount = 2,
                        ColorCount = 1,
                        IsOpenRoad = true,
                        HasSecrets = false,
                        HasLocks = false,
                        Create = CreateArchetypeLevel1
                    },
                    new ArchetypeDef
                    {
                        Id = 2,
                        Title = "Mẫu 2: Vòng khép 3 hộp (Level 2)",
                        Description = "Vòng khép kín bo góc quanh 3 hộp ngang, 2 màu đối xứng",
                        BoxCount = 3,
                        ColorCount = 2,
                        IsOpenRoad = false,
                        HasSecrets = false,
                        HasLocks = false,
                        Create = CreateArchetypeLevel2
                    },
                    new ArchetypeDef
                    {
                        Id = 3,
                        Title = "Mẫu 3: Vòng rộng 4 hộp (Level 3)",
                        Description = "Vòng chữ nhật bo tròn rộng, 4 hộp ở 4 góc đối xứng, 3 màu",
                        BoxCount = 4,
                        ColorCount = 3,
                        IsOpenRoad = false,
                        HasSecrets = false,
                        HasLocks = false,
                        Create = CreateArchetypeLevel3
                    },
                    new ArchetypeDef
                    {
                        Id = 4,
                        Title = "Mẫu 4: 2 Dãy đối xứng 5 hộp (Level 5)",
                        Description = "2 dãy hộp trên/dưới quay vào nhau quanh vòng oval, 4 màu",
                        BoxCount = 5,
                        ColorCount = 4,
                        IsOpenRoad = false,
                        HasSecrets = false,
                        HasLocks = false,
                        Create = CreateArchetypeLevel5
                    },
                    new ArchetypeDef
                    {
                        Id = 5,
                        Title = "Mẫu 5: Vòng đa giác 6 hộp + Khối ẩn (Level 6)",
                        Description = "Vòng 16 điểm bo tròn, 6 hộp hướng tâm, giới thiệu khối ẩn ở tầng đáy",
                        BoxCount = 6,
                        ColorCount = 5,
                        IsOpenRoad = false,
                        HasSecrets = true,
                        HasLocks = false,
                        Create = CreateArchetypeLevel6
                    },
                    new ArchetypeDef
                    {
                        Id = 6,
                        Title = "Mẫu 6: 2 Dãy 7 hộp đối xứng (Level 10)",
                        Description = "2 dãy 3 hộp trên và 4 hộp dưới đối diện, vòng oval, khối ẩn",
                        BoxCount = 7,
                        ColorCount = 6,
                        IsOpenRoad = false,
                        HasSecrets = true,
                        HasLocks = false,
                        Create = CreateArchetypeLevel10
                    },
                    new ArchetypeDef
                    {
                        Id = 7,
                        Title = "Mẫu 7: Cột dọc 8 hộp + Tuyến mở (Level 12)",
                        Description = "1 cột dọc 8 hộp quay 270°, băng chuyền thẳng dọc có Cổng Vào & Ra",
                        BoxCount = 8,
                        ColorCount = 6,
                        IsOpenRoad = true,
                        HasSecrets = true,
                        HasLocks = false,
                        Create = CreateArchetypeLevel12
                    },
                    new ArchetypeDef
                    {
                        Id = 8,
                        Title = "Mẫu 8: Tuyến mở chữ S 7 hộp (Level 13)",
                        Description = "Băng chuyền uốn lượn chữ S có Cổng Vào/Ra, 7 hộp bao quanh",
                        BoxCount = 7,
                        ColorCount = 6,
                        IsOpenRoad = true,
                        HasSecrets = false,
                        HasLocks = false,
                        Create = CreateArchetypeLevel13
                    },
                    new ArchetypeDef
                    {
                        Id = 9,
                        Title = "Mẫu 9: Vòng chữ nhật lớn 8 hộp (Level 15)",
                        Description = "Vòng chữ nhật lớn bo góc, 8 hộp viền quanh hướng ra đường, khối ẩn",
                        BoxCount = 8,
                        ColorCount = 7,
                        IsOpenRoad = false,
                        HasSecrets = true,
                        HasLocks = false,
                        Create = CreateArchetypeLevel15
                    },
                    new ArchetypeDef
                    {
                        Id = 10,
                        Title = "Mẫu 10: Tuyến mở uốn lượn 9 hộp (Level 20)",
                        Description = "Băng chuyền uốn lượn nhiều đoạn, 9 hộp, Cổng Vào & Ra, khối ẩn",
                        BoxCount = 9,
                        ColorCount = 8,
                        IsOpenRoad = true,
                        HasSecrets = true,
                        HasLocks = false,
                        Create = CreateArchetypeLevel20
                    },
                    new ArchetypeDef
                    {
                        Id = 11,
                        Title = "Mẫu 11: Vòng 5 hộp + Khóa màu (Level 21)",
                        Description = "Vòng khép kín, 5 hộp, giới thiệu 2 hộp khóa màu",
                        BoxCount = 5,
                        ColorCount = 4,
                        IsOpenRoad = false,
                        HasSecrets = false,
                        HasLocks = true,
                        Create = CreateArchetypeLevel21
                    },
                    new ArchetypeDef
                    {
                        Id = 12,
                        Title = "Mẫu 12: Vòng 9 hộp + Khóa + Ẩn (Level 25)",
                        Description = "9 hộp với 4 hộp khóa màu kết hợp khối ẩn dưới đáy",
                        BoxCount = 9,
                        ColorCount = 8,
                        IsOpenRoad = false,
                        HasSecrets = true,
                        HasLocks = true,
                        Create = CreateArchetypeLevel25
                    },
                    new ArchetypeDef
                    {
                        Id = 13,
                        Title = "Mẫu 13: Tuyến mở 8 hộp nâng cao (Level 30)",
                        Description = "Băng chuyền mở có Cổng Vào/Ra, 4 hộp khóa màu, khối ẩn",
                        BoxCount = 8,
                        ColorCount = 7,
                        IsOpenRoad = true,
                        HasSecrets = true,
                        HasLocks = true,
                        Create = CreateArchetypeLevel30
                    }
                };
            }
            return _all;
        }
    }

    private static JObject CreateBase(int levelNumber, int pathLength, float size = 10f)
    {
        JObject root = A.NewLevel(levelNumber);
        JObject layout = A.Layout(root);
        layout["PathLength"] = pathLength;
        layout["PathSectionDistance"] = 0.2f;
        layout["Size"] = size;
        return root;
    }

    public static JObject CreateArchetypeLevel1(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 4, 8f);
        JObject layout = A.Layout(root);

        // Road
        Vector3 p0 = new Vector3(-2.8f, 0.2f, 2.2f);
        Vector3 p1 = new Vector3(2.8f, 0.2f, 2.2f);
        layout["Paths"] = new JArray(A.StraightRoad(p0, p1));

        // Boxes
        JArray boxes = A.Array(layout, "Boxes");
        JObject b0 = A.NewBox(new Vector3(-0.8f, 0f, -0.6f));
        b0["Cubes"] = new JArray(A.NewCube(3), A.NewCube(3));
        b0["PathIndex"] = 5;
        b0["PathIndexV2"] = 1;
        boxes.Add(b0);

        JObject b1 = A.NewBox(new Vector3(0.8f, 0f, -0.6f));
        b1["Cubes"] = new JArray(A.NewCube(3), A.NewCube(3));
        b1["PathIndex"] = 9;
        b1["PathIndexV2"] = 15;
        boxes.Add(b1);

        A.SyncEnterExitGates(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel2(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 8, 10f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        JObject b0 = A.NewBox(new Vector3(-1.6f, 0f, -0.07f));
        b0["Cubes"] = new JArray(A.NewCube(1), A.NewCube(2));
        boxes.Add(b0);

        JObject b1 = A.NewBox(new Vector3(0f, 0f, 0.54f));
        b1["Cubes"] = new JArray(A.NewCube(1), A.NewCube(2));
        boxes.Add(b1);

        JObject b2 = A.NewBox(new Vector3(1.6f, 0f, -0.07f));
        b2["Cubes"] = new JArray(A.NewCube(1), A.NewCube(1), A.NewCube(2), A.NewCube(2));
        boxes.Add(b2);

        layout["Paths"] = new JArray(A.RoundedRectangleRoad(new Vector3(0f, 0.2f, 0.2f), 8f, 6.5f, 1.5f));
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel3(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 7, 10f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        boxes.Add(CreateBoxWithCubes(new Vector3(-0.78f, 0f, 0.8f), Vector3.zero, new[] { 4, 4, 2, 1 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(0.78f, 0f, 0.8f), Vector3.zero, new[] { 1, 1, 2 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(-2.5f, 0f, -0.8f), Vector3.zero, new[] { 4 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(2.5f, 0f, -0.8f), Vector3.zero, new[] { 2, 2, 1, 4 }));

        layout["Paths"] = new JArray(A.RoundedRectangleRoad(Vector3.zero, 9.5f, 7.5f, 2.0f));
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel5(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 9, 12f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        boxes.Add(CreateBoxWithCubes(new Vector3(0f, 0f, 0f), new Vector3(0, 180, 0), new[] { 1 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(-2.3f, 0f, 5.0f), new Vector3(0, 180, 0), new[] { 3, 3, 4, 2 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(-2.6f, 0f, -4.8f), Vector3.zero, new[] { 2, 3, 3, 1 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(2.3f, 0f, 5.0f), new Vector3(0, 180, 0), new[] { 2, 4, 1, 2 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(2.6f, 0f, -4.8f), Vector3.zero, new[] { 4, 1, 4 }));

        layout["Paths"] = new JArray(A.OvalRoad(Vector3.zero, 9.5f, 13.5f));
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel6(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 6, 12f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        var positions = new[]
        {
            new Vector3(-2.84f, 0f, 3.09f),
            new Vector3(1.63f, 0f, 4.59f),
            new Vector3(2.81f, 0f, 3.32f),
            new Vector3(-2.12f, 0f, -4.61f),
            new Vector3(1.65f, 0f, -2.86f),
            new Vector3(-1.51f, 0f, 0.36f)
        };
        var yaws = new[] { 226f, 226f, 226f, 136f, 225f, 226f };
        for (int i = 0; i < positions.Length; i++)
        {
            boxes.Add(A.NewBox(positions[i]));
            ((JObject)boxes[i])["Rotation"] = A.Vector(new Vector3(0, yaws[i], 0));
        }

        layout["Paths"] = new JArray(A.OvalRoad(Vector3.zero, 10.5f, 12f));
        LoopLevelCommands.DistributeSolvableCubes(root, null, 5, 12345, 50);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel10(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 6, 12f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        // 3 top boxes facing 180
        boxes.Add(A.NewBox(new Vector3(-2.0f, 0f, 1.1f)));
        boxes.Add(A.NewBox(new Vector3(0.0f, 0f, 2.77f)));
        boxes.Add(A.NewBox(new Vector3(2.0f, 0f, 1.1f)));

        // 4 bottom boxes facing 0
        boxes.Add(A.NewBox(new Vector3(-3.0f, 0f, -4.7f)));
        boxes.Add(A.NewBox(new Vector3(-1.0f, 0f, -4.7f)));
        boxes.Add(A.NewBox(new Vector3(1.0f, 0f, -4.7f)));
        boxes.Add(A.NewBox(new Vector3(3.0f, 0f, -4.7f)));

        layout["Paths"] = new JArray(A.OvalRoad(Vector3.zero, 11f, 13f));
        LoopLevelCommands.DistributeSolvableCubes(root, null, 6, 54321, 50);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel12(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 4, 15f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        float[] zPositions = { 5.94f, 4.23f, 2.53f, 0.80f, -0.91f, -2.62f, -4.23f, -5.92f };
        for (int i = 0; i < zPositions.Length; i++)
        {
            JObject b = A.NewBox(new Vector3(1.01f, 0f, zPositions[i]));
            b["Rotation"] = A.Vector(new Vector3(0, 270f, 0));
            boxes.Add(b);
        }

        Vector3 p0 = new Vector3(-2.2f, 0.2f, 7.1f);
        Vector3 p1 = new Vector3(-2.2f, 0.2f, -6.9f);
        layout["Paths"] = new JArray(A.StraightRoad(p0, p1));

        A.SyncEnterExitGates(layout);
        LoopLevelCommands.DistributeSolvableCubes(root, null, 6, 9999, 50);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel13(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 5, 12f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        boxes.Add(A.NewBox(new Vector3(-2.5f, 0f, 3.5f)));
        boxes.Add(A.NewBox(new Vector3(-0.5f, 0f, 3.5f)));
        boxes.Add(A.NewBox(new Vector3(1.5f, 0f, 3.5f)));
        boxes.Add(A.NewBox(new Vector3(-2.0f, 0f, -3.0f)));
        boxes.Add(A.NewBox(new Vector3(0.0f, 0f, -3.0f)));
        boxes.Add(A.NewBox(new Vector3(2.0f, 0f, -3.0f)));
        boxes.Add(A.NewBox(new Vector3(3.5f, 0f, 0.0f)));

        layout["Paths"] = new JArray(A.SCurveRoad(Vector3.zero, 8f, 10f, 0.6f));
        A.SyncEnterExitGates(layout);
        LoopLevelCommands.DistributeSolvableCubes(root, null, 6, 7777, 40);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel15(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 5, 13f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        // Perimeter boxes
        boxes.Add(A.NewBox(new Vector3(-3.2f, 0f, 4.2f)));
        boxes.Add(A.NewBox(new Vector3(0f, 0f, 4.2f)));
        boxes.Add(A.NewBox(new Vector3(3.2f, 0f, 4.2f)));
        boxes.Add(A.NewBox(new Vector3(-3.2f, 0f, -4.2f)));
        boxes.Add(A.NewBox(new Vector3(0f, 0f, -4.2f)));
        boxes.Add(A.NewBox(new Vector3(3.2f, 0f, -4.2f)));
        boxes.Add(A.NewBox(new Vector3(-3.5f, 0f, 0f)));
        boxes.Add(A.NewBox(new Vector3(3.5f, 0f, 0f)));

        layout["Paths"] = new JArray(A.RoundedRectangleRoad(Vector3.zero, 12f, 12f, 2.5f));
        LoopLevelCommands.DistributeSolvableCubes(root, null, 7, 1515, 60);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel20(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 6, 14f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        for (int i = 0; i < 9; i++)
        {
            float angle = i * Mathf.PI * 2f / 9f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 3.5f, 0f, Mathf.Sin(angle) * 3.5f);
            boxes.Add(A.NewBox(pos));
        }

        layout["Paths"] = new JArray(A.OpenWindingRoad(Vector3.zero, 10f, 12f, 0.6f));
        A.SyncEnterExitGates(layout);
        LoopLevelCommands.DistributeSolvableCubes(root, null, 8, 2020, 60);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel21(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 9, 12f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        // Box 0 & 1: locked by Color 5
        JObject b0 = CreateBoxWithCubes(new Vector3(-1.55f, 0f, 0.38f), new Vector3(0, 225f, 0), new[] { 3, 3, 1, 1 });
        b0["LockColor"] = 5;
        boxes.Add(b0);

        JObject b1 = CreateBoxWithCubes(new Vector3(1.73f, 0f, -2.77f), new Vector3(0, 225f, 0), new[] { 1, 1, 3, 3 });
        b1["LockColor"] = 5;
        boxes.Add(b1);

        // Boxes 2, 3, 4: unlocked, contain colors 5 and 4
        boxes.Add(CreateBoxWithCubes(new Vector3(-2.42f, 0f, -4.64f), new Vector3(0, 315f, 0), new[] { 5, 5 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(2.47f, 0f, 2.1f), new Vector3(0, 315f, 0), new[] { 4, 4, 5, 5 }));
        boxes.Add(CreateBoxWithCubes(new Vector3(-2.25f, 0f, 3.71f), new Vector3(0, 225f, 0), new[] { 4, 4 }));

        layout["Paths"] = new JArray(A.OvalRoad(Vector3.zero, 10.5f, 12.5f));
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel25(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 6, 14f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        for (int i = 0; i < 9; i++)
        {
            float angle = i * Mathf.PI * 2f / 9f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 3.8f, 0f, Mathf.Sin(angle) * 3.8f);
            boxes.Add(A.NewBox(pos));
        }

        // Color locks on 4 boxes
        ((JObject)boxes[0])["LockColor"] = 1;
        ((JObject)boxes[2])["LockColor"] = 2;
        ((JObject)boxes[4])["LockColor"] = 3;
        ((JObject)boxes[6])["LockColor"] = 4;

        layout["Paths"] = new JArray(A.OvalRoad(Vector3.zero, 11f, 11f));
        LoopLevelCommands.DistributeSolvableCubes(root, null, 8, 2525, 50);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    public static JObject CreateArchetypeLevel30(int levelNumber)
    {
        JObject root = CreateBase(levelNumber, 6, 14f);
        JObject layout = A.Layout(root);

        JArray boxes = A.Array(layout, "Boxes");
        for (int i = 0; i < 8; i++)
        {
            float z = 5.0f - i * 1.5f;
            JObject b = A.NewBox(new Vector3(1.2f, 0f, z));
            b["Rotation"] = A.Vector(new Vector3(0, 270f, 0));
            boxes.Add(b);
        }

        // Color locks on 4 boxes
        ((JObject)boxes[0])["LockColor"] = 1;
        ((JObject)boxes[2])["LockColor"] = 2;
        ((JObject)boxes[4])["LockColor"] = 3;
        ((JObject)boxes[6])["LockColor"] = 4;

        layout["Paths"] = new JArray(A.SCurveRoad(new Vector3(-1.8f, 0, 0), 6f, 13f, 0.6f));
        A.SyncEnterExitGates(layout);
        LoopLevelCommands.DistributeSolvableCubes(root, null, 7, 3030, 40);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        return root;
    }

    private static JObject CreateBoxWithCubes(Vector3 pos, Vector3 rot, int[] cubeTypes)
    {
        JObject box = A.NewBox(pos);
        box["Rotation"] = A.Vector(rot);
        var cubes = new JArray();
        for (int i = 0; i < cubeTypes.Length; i++)
        {
            cubes.Add(A.NewCube(cubeTypes[i]));
        }
        box["Cubes"] = cubes;
        return box;
    }
}
#endif

