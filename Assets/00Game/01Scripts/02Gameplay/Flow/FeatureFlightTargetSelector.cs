using System.Collections.Generic;
using UnityEngine;

internal static class FeatureFlightTargetSelector
{
    public static FeatureFlightTarget Select(FeatureType feature, GameController controller,
        IReadOnlyList<Box> boxes, RectTransform overlay, Camera overlayCamera)
    {
        if (boxes == null || controller == null || controller.MainCamera == null) return null;
        for (int i = 0; i < boxes.Count; i++)
        {
            Box box = boxes[i];
            if (box == null || box.Info == null || !box.IsEntrancePresentationReady) continue;
            FeatureFlightTarget target = Create(feature, box, controller, boxes);
            if (target != null && target.TryGetPose(controller.MainCamera, overlay, overlayCamera, out _)) return target;
        }
        return null;
    }

    private static FeatureFlightTarget Create(FeatureType feature, Box box, GameController controller,
        IReadOnlyList<Box> boxes)
    {
        BoxInfo info = box.Info;
        if (feature == FeatureType.GroupColorLock)
        {
            GroupInfo group = info.GroupInfo;
            if (group == null || !group.IsLocked || !controller.GetFeatureGroupDoor(group.Id, out GroupRollDoor door) ||
                door == null || door.FeatureLockVisual == null) return null;
            return new FeatureFlightTarget(box, door.FeatureLockVisual);
        }
        // A group door covers its slots; do not aim through it at another feature.
        if (info.GroupInfo != null && info.GroupInfo.IsLocked) return null;
        switch (feature)
        {
            case FeatureType.ColorLock:
                if (info.LockColor > 0) return FromComponent<ColorLock>(box);
                break;
            case FeatureType.NumLock:
                if (info.NumLock > 0) return FromComponent<BoxNumLock>(box);
                break;
            case FeatureType.AdjacentLock:
                if (info.IsAdjacentLocked) return FromComponent<AdjacentLock>(box);
                break;
            case FeatureType.EnterOnly:
                if (info.IsEnterOnly) return FromVisual(box, HierarchyReferenceCache.Descendant(box.transform, "Arrow"));
                break;
            case FeatureType.LimitedColor:
                if (info.LimitedColor > 0) return FromVisual(box, HierarchyReferenceCache.Child(box.transform, "Guide"));
                break;
            case FeatureType.SingleBox:
                if (info.BoxType == BoxType.Single) return FromVisual(box, box.BoxCard);
                break;
            case FeatureType.LongBox:
                if (info.BoxType == BoxType.Long) return FromVisual(box, box.BoxCard);
                break;
            case FeatureType.LinkedCubes:
            case FeatureType.LinkedHiddenCubes:
                return FromPair(feature, box, controller, boxes);
            default:
                return FromCube(feature, box);
        }
        return null;
    }

    private static FeatureFlightTarget FromComponent<T>(Box box) where T : Component
    {
        T component = box.GetComponentInChildren<T>();
        if (component == null) return null;
        return FromVisual(box, component.transform);
    }

    private static FeatureFlightTarget FromVisual(Box box, Transform visual)
    {
        if (visual == null || !visual.gameObject.activeInHierarchy) return null;
        return new FeatureFlightTarget(box, visual);
    }

    private static FeatureFlightTarget FromCube(FeatureType feature, Box box)
    {
        if (box.Info.HasBlockingLock()) return null;
        CubeInfo info = box.Info.PeekTopCube();
        if (info == null || info.CubeSc == null || info.CubeSc.VisibleSize <= 0) return null;
        Cube cube = info.CubeSc;
        Transform visual = null;
        switch (feature)
        {
            case FeatureType.SecretCube:
                if (info.IsSecret && cube.FeatureIcon != null) visual = cube.FeatureIcon.transform;
                break;
            case FeatureType.SurpriseCube:
                if (!info.IsSecret && CubeData.IsSurpriseColor(info.Type) && cube.FeatureIcon != null)
                    visual = cube.FeatureIcon.transform;
                break;
            case FeatureType.CubeNumLock:
                CubeNumLock ice = cube.GetComponentInChildren<CubeNumLock>();
                if (info.NumLock > 0 && ice != null) visual = ice.transform;
                break;
        }
        if (visual == null || !visual.gameObject.activeInHierarchy) return null;
        return new FeatureFlightTarget(box, visual, cube);
    }

    private static FeatureFlightTarget FromPair(FeatureType feature, Box box, GameController controller,
        IReadOnlyList<Box> boxes)
    {
        LinkedCubePair pair = box.Info.LinkedPair;
        if (pair == null || pair.State != LinkedCubePairState.Pending || pair.FirstBox != box.Info) return null;
        CubeInfo first = pair.FirstCube;
        CubeInfo second = pair.SecondCube;
        if (!IsExposedMember(pair.FirstBox, first) || !IsExposedMember(pair.SecondBox, second)) return null;
        bool hidden = first.IsSecret || second.IsSecret;
        if (feature == FeatureType.LinkedHiddenCubes && !hidden) return null;
        if (feature == FeatureType.LinkedCubes && hidden) return null;
        Box partner = GetBox(boxes, pair.SecondBox);
        if (partner == null || !partner.IsEntrancePresentationReady) return null;
        LinkedCubeCable cable = controller.GetLinkedCable(box.Info);
        Transform cableVisual = null;
        // Hidden pairs can use visible markers, never reveal the suppressed cable.
        if (!hidden && cable != null) cableVisual = cable.transform;
        return new FeatureFlightTarget(box, first.CubeSc.FeatureIcon.transform, first.CubeSc,
            partner, second.CubeSc.FeatureIcon.transform, cableVisual);
    }

    private static bool IsExposedMember(BoxInfo box, CubeInfo cube)
    {
        if (box == null || cube == null || box.HasBlockingLock() || box.PeekTopCube() != cube) return false;
        if (cube.CubeSc == null || cube.CubeSc.VisibleSize <= 0 || cube.CubeSc.FeatureIcon == null) return false;
        SpriteRenderer marker = cube.CubeSc.FeatureIcon;
        return marker.enabled && marker.gameObject.activeInHierarchy;
    }

    private static Box GetBox(IReadOnlyList<Box> boxes, BoxInfo info)
    {
        for (int i = 0; i < boxes.Count; i++)
            if (boxes[i] != null && boxes[i].Info == info) return boxes[i];
        return null;
    }
}

