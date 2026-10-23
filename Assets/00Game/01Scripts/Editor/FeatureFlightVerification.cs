using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Runs feature-target and UI projection checks with Unity's actual rendering types.</summary>
public static class FeatureFlightVerification
{
    private static readonly List<GameObject> Objects = new List<GameObject>();
    private static readonly Assembly Runtime = typeof(GameController).Assembly;
    private static int _checks;
    private static Scene _previewScene;

    [MenuItem("Tools/LoopMaster/Verify Feature Unlock Flight")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run feature flight verification outside Play Mode.");
        _checks = 0;
        _previewScene = EditorSceneManager.NewPreviewScene();
        bool passed = false;
        try
        {
            VerifySourceProjection();
            VerifySelection();
            VerifyPrefabBindings();
            Debug.Log("Feature flight verification passed: " + _checks + " checks (EditMode; animation not exercised).");
            passed = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            for (int i = Objects.Count - 1; i >= 0; i--)
                if (Objects[i] != null) Object.DestroyImmediate(Objects[i]);
            Objects.Clear();
            EditorSceneManager.ClosePreviewScene(_previewScene);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static GameObject Create(string name)
    {
        var instance = new GameObject(name);
        instance.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(instance, _previewScene);
        Objects.Add(instance);
        return instance;
    }

    private static void VerifySourceProjection()
    {
        GameObject root = Create("FlightProjectionCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform overlay = (RectTransform)root.transform;
        GameObject item = Create("SourceIcon");
        item.transform.SetParent(root.transform, false);
        Image source = item.AddComponent<Image>();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/00Game/06OutResource/Game/Sprite/Tutorial/img_tutorial_linkedCubes.png");
        Expect(sprite != null, "linked tutorial sprite exists");
        source.sprite = sprite;
        source.preserveAspect = true;
        source.rectTransform.sizeDelta = new Vector2(310f, 180f);
        source.rectTransform.pivot = new Vector2(.1f, .8f);
        source.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 23f);
        Canvas.ForceUpdateCanvases();
        Type projection = Runtime.GetType("FeatureFlightProjection", true);
        object[] arguments = { source, overlay, null, null };
        Expect((bool)projection.GetMethod("TryCapture").Invoke(null, arguments), "capture rotated noncentral UI icon");
        object pose = arguments[3];
        Vector2 size = (Vector2)pose.GetType().GetField("Size").GetValue(pose);
        float angle = (float)pose.GetType().GetField("Angle").GetValue(pose);
        Expect(Mathf.Abs(size.x / size.y - sprite.rect.width / sprite.rect.height) < .001f,
            "preserveAspect keeps the sprite ratio");
        Expect(Mathf.Abs(Mathf.DeltaAngle(angle, 23f)) < .01f, "source rotation survives projection");
        Vector2 center = (Vector2)pose.GetType().GetField("Center").GetValue(pose);
        Rect fitted = source.GetPixelAdjustedRect();
        Vector2 original = fitted.size;
        float ratio = Mathf.Min(original.x / sprite.rect.width, original.y / sprite.rect.height);
        fitted.size = sprite.rect.size * ratio;
        fitted.position += Vector2.Scale(original - fitted.size, source.rectTransform.pivot);
        Vector3 expected = overlay.InverseTransformPoint(source.rectTransform.TransformPoint(fitted.center));
        Expect(Vector2.Distance(center, expected) < .01f, "preserveAspect respects the Image pivot");
        source.enabled = false;
        arguments[3] = null;
        Expect(!(bool)projection.GetMethod("TryCapture").Invoke(null, arguments), "disabled source is rejected");
    }

    private static void VerifySelection()
    {
        GameObject controllerObject = Create("FeatureController");
        GameController controller = controllerObject.AddComponent<GameController>();
        Camera camera = Create("FeatureCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.transform.position = new Vector3(0f, 0f, -20f);
        camera.pixelRect = new Rect(0f, 0f, 1080f, 1920f);
        Set(controller, "_mainCamera", camera);
        Canvas canvas = Create("FeatureSelectionCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.ForceUpdateCanvases();
        RectTransform overlay = (RectTransform)canvas.transform;

        Box first = CreateBox("First", BoxType.Single);
        Box second = CreateBox("Second", BoxType.Single);
        var boxes = new List<Box> { first, second };
        object selected = Select(FeatureType.SingleBox, controller, boxes, overlay);
        Expect(selected != null && GetOwner(selected) == first, "first valid same-feature instance wins");
        first.gameObject.SetActive(false);
        selected = Select(FeatureType.SingleBox, controller, boxes, overlay);
        Expect(selected != null && GetOwner(selected) == second, "inactive candidate is skipped");
        Expect(Select(FeatureType.LongBox, controller, boxes, overlay) == null, "wrong feature has no fallback target");
        Set(second, "_entrancePending", true);
        Expect(Select(FeatureType.SingleBox, controller, boxes, overlay) == null, "unfinished entrance is rejected");
        Set(second, "_entrancePending", false);
        second.transform.position = new Vector3(1000f, 0f, 0f);
        Expect(Select(FeatureType.SingleBox, controller, boxes, overlay) == null, "offscreen target is rejected");
        second.transform.position = Vector3.zero;
        second.BoxCard.GetComponent<Renderer>().enabled = false;
        Expect(Select(FeatureType.SingleBox, controller, boxes, overlay) == null, "disabled visual is rejected");
        second.BoxCard.GetComponent<Renderer>().enabled = true;
        selected = Select(FeatureType.SingleBox, controller, boxes, overlay);
        int version = (int)typeof(Box).GetField("_routineVersion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(second);
        Set(second, "_routineVersion", version + 1);
        object[] pose = { camera, overlay, null, null };
        Expect(!(bool)selected.GetType().GetMethod("TryGetPose").Invoke(selected, pose), "stale target rejected after reset version changes");
        VerifyFeatureMappings(controller, overlay);
    }

    private static void VerifyFeatureMappings(GameController controller, RectTransform overlay)
    {
        Box slot = CreateBox("SlotFeatures", BoxType.Long);
        slot.Info.LockColor = 1;
        slot.Info.NumLock = 2;
        slot.Info.IsAdjacentLocked = true;
        slot.Info.IsEnterOnly = true;
        slot.Info.LimitedColor = 1;
        slot.BoxCard.gameObject.AddComponent<ColorLock>();
        slot.BoxCard.gameObject.AddComponent<BoxNumLock>();
        slot.BoxCard.gameObject.AddComponent<AdjacentLock>();
        AddQuad(slot.transform, "Arrow");
        AddQuad(slot.transform, "Guide");
        FeatureType[] slotFeatures = { FeatureType.ColorLock, FeatureType.NumLock, FeatureType.AdjacentLock,
            FeatureType.EnterOnly, FeatureType.LimitedColor, FeatureType.LongBox };
        var boxes = new List<Box> { slot };
        for (int i = 0; i < slotFeatures.Length; i++)
            Expect(Select(slotFeatures[i], controller, boxes, overlay) != null, "slot mapping: " + slotFeatures[i]);

        var scene = new GameplaySceneBuilder();
        Set(controller, "_sceneBuilder", scene);
        var group = new GroupInfo(new GroupData { Id = 17, BoxIndexes = new List<int> { 0 } });
        slot.Info.GroupInfo = group;
        GroupRollDoor door = Create("GroupDoor").AddComponent<GroupRollDoor>();
        Set(door, "_keyLockRoot", AddQuad(door.transform, "GroupLock").transform);
        var doors = (Dictionary<int, GroupRollDoor>)typeof(GameplaySceneBuilder)
            .GetField("_groupRollDoors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scene);
        doors.Add(group.Id, door);
        Expect(Select(FeatureType.GroupColorLock, controller, boxes, overlay) != null, "group maps to its registered door");
        Expect(Select(FeatureType.ColorLock, controller, boxes, overlay) == null, "door conceals the covered slot feature");

        Box first = CreateBox("CubeFeatures", BoxType.Normal);
        Cube cube = AddCube(first);
        boxes = new List<Box> { first };
        cube.Info.IsSecret = true;
        Expect(Select(FeatureType.SecretCube, controller, boxes, overlay) != null, "secret maps to visible marker");
        cube.Info.IsSecret = false;
        cube.Info.Type = 201;
        Expect(Select(FeatureType.SurpriseCube, controller, boxes, overlay) != null, "surprise maps to its visible cube");
        cube.Info.NumLock = 2;
        cube.GetComponentInChildren<SpriteRenderer>().gameObject.AddComponent<CubeNumLock>();
        Expect(Select(FeatureType.CubeNumLock, controller, boxes, overlay) != null, "frozen cube maps to its ice visual");
        cube.Info.NumLock = 0;
        cube.Info.Type = 1;
        Box partner = CreateBox("LinkedPartner", BoxType.Normal);
        partner.transform.position = new Vector3(2f, 0f, 0f);
        Cube partnerCube = AddCube(partner);
        cube.Info.LinkedPairId = partnerCube.Info.LinkedPairId = 1;
        Activator.CreateInstance(typeof(LinkedCubePair), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { 1, first.Info, 0, partner.Info, 1, LinkedCubePairState.Pending }, null);
        boxes.Add(partner);
        Expect(Select(FeatureType.LinkedCubes, controller, boxes, overlay) != null, "linked feature maps to its exposed pair");
        partnerCube.Info.IsSecret = true;
        Expect(Select(FeatureType.LinkedHiddenCubes, controller, boxes, overlay) != null, "hidden linked uses exposed markers");
        Expect(Select(FeatureType.LinkedCubes, controller, boxes, overlay) == null, "hidden pair is not selected as a normal pair");
        partnerCube.gameObject.SetActive(false);
        Expect(Select(FeatureType.LinkedHiddenCubes, controller, boxes, overlay) == null, "hidden partner is not revealed by flight");
    }

    private static GameObject AddQuad(Transform parent, string name)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(quad, _previewScene);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        return quad;
    }

    private static Cube AddCube(Box box)
    {
        GameObject root = Create("Cube");
        root.transform.SetParent(box.transform, false);
        Cube cube = root.AddComponent<Cube>();
        var info = new CubeInfo(1, GameData.SliceCountPerGroup);
        info.CubeSc = cube;
        box.Info.CubeInfos.Add(info);
        box.Cubes.Add(cube);
        SpriteRenderer marker = Create("Marker").AddComponent<SpriteRenderer>();
        marker.transform.SetParent(root.transform, false);
        marker.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/00Game/06OutResource/Game/Sprite/Tutorial/img_tutorial_linkedCubes.png");
        marker.transform.localScale = Vector3.one * (.8f / Mathf.Max(marker.sprite.bounds.size.x, marker.sprite.bounds.size.y));
        Set(cube, "_info", info);
        Set(cube, "_box", box);
        Set(cube, "_square", marker);
        return cube;
    }

    private static Box CreateBox(string name, BoxType type)
    {
        GameObject instance = Create(name);
        Box box = instance.AddComponent<Box>();
        var info = new BoxInfo(new BoxData { BoxType = type }, null);
        Set(box, "_info", info);
        GameObject card = AddQuad(instance.transform, "BoxCard");
        Set(box, "_boxCard", card.transform);
        return box;
    }

    private static object Select(FeatureType feature, GameController controller, IReadOnlyList<Box> boxes,
        RectTransform overlay)
    {
        return Runtime.GetType("FeatureFlightTargetSelector", true).GetMethod("Select")
            .Invoke(null, new object[] { feature, controller, boxes, overlay, null });
    }

    private static Box GetOwner(object target)
    {
        return (Box)target.GetType().GetField("_box", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void VerifyPrefabBindings()
    {
        GameObject prefab = Resources.Load<GameObject>("BoosterPresentation");
        Expect(prefab != null && prefab.GetComponent<BoosterPresentationUI>() != null, "existing flight view is available");
        GameObject popup = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/00Game/04Prefabs/Gameplay/UI/Dialogs/FeatureUnlockDialog.prefab");
        Expect(popup != null, "feature popup exists");
        var serialized = new SerializedObject(popup.GetComponent<FeatureUnlockDialog>());
        string[] fields = { "_colorLockSprite", "_enterOnlySprite", "_secretCubeSprite", "_numLockSprite",
            "_groupColorLockSprite", "_limitColorSprite", "_adjacentLockSprite", "_singleBoxSprite",
            "_longSlotSprite", "_cubeNumLockSprite", "_surpriseCubeSprite", "_linkedCubesSprite", "_linkedHiddenCubesSprite" };
        for (int i = 0; i < fields.Length; i++)
            Expect(serialized.FindProperty(fields[i]).objectReferenceValue != null, "feature art bound: " + fields[i]);
        GameObject enter = Resources.Load<GameObject>("CoreGamePrefabs/OnlyEnterBox");
        GameObject limited = Resources.Load<GameObject>("CoreGamePrefabs/LimitedColorBox");
        Expect(enter != null && HierarchyReferenceCache.Descendant(enter.transform, "Arrow") != null,
            "enter-only visual binding exists");
        Expect(limited != null && HierarchyReferenceCache.Child(limited.transform, "Guide") != null,
            "limited-color visual binding exists");
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Expect(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Feature flight verification failed: " + name);
        _checks++;
    }
}
