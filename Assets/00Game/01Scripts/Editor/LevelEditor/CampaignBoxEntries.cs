#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using A = LoopLevelAuthoring;

/// <summary>Keeps campaign boxes close to separate conveyor entry points.</summary>
public static class CampaignBoxEntries
{
    public const float MaximumEntryDistance = 1.6f;

    /// <summary>Rejects shared entry slots and long flights from box mouths.</summary>
    public static List<string> GetProblems(JObject root)
    {
        var problems = new List<string>();
        var used = new HashSet<int>();
        List<Vector3> slots = A.Slots(root);
        JArray boxes = A.Array(A.Layout(root), "Boxes");
        for (int index = 0; index < boxes.Count; index++)
        {
            JObject box = (JObject)boxes[index];
            int slot = A.Int(box, "PathIndexV2") - 1;
            if (slot < 0 || slot >= slots.Count)
            {
                problems.Add("Box " + index + " has an invalid entry slot.");
                continue;
            }
            if (!used.Add(slot)) problems.Add("Box " + index + " shares an entry slot.");
            if (A.Int(box, "PathIndex") != slot) problems.Add("Box " + index + " has inconsistent entry indexes.");
            bool single = A.Int(box, "BoxType") == 1;
            Quaternion rotation = Quaternion.Euler(A.Vector(box, "Rotation"));
            Vector3 mouth = A.Vector(box, "Position") + rotation * new Vector3(0, 0, single ? -.2f : 2.2f);
            Vector3 delta = slots[slot] - mouth;
            delta.y = 0;
            if (delta.magnitude > MaximumEntryDistance)
                problems.Add("Box " + index + " entry flight is " + delta.magnitude.ToString("F2") + " units.");
        }
        return problems;
    }

    /// <summary>Replaces unsafe layouts with two outward-facing banks beside the road.</summary>
    public static bool Repair(JObject root, int preferredRoad)
    {
        if (GetProblems(root).Count == 0) return false;
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");
        List<int> order = BuildBankOrder(layout, boxes);
        LoopLevelBoardLayout.ArrangeTwoColumnsFacingOutward(boxes, order, .35f, .35f);
        var settings = new LoopLevelGeneratorSettings
        {
            RoadShape = preferredRoad < 3 ? 0 : 4,
            RoadClearance = .08f
        };
        LoopLevelBoardLayout.FitRoadOnly(layout, settings);
        LoopLevelBoardLayout.AutoBindBoxesToRoad(layout);
        Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, true);
        foreach (JObject item in A.Array(layout, "MapData"))
            if (item.Value<string>("Name") == "Capacity")
                item["Position"] = A.Vector(new Vector3(bounds.center.x, .2f, bounds.max.z + 1.2f));
        bounds = LoopLevelBoardLayout.BoardBounds(layout, true);
        layout["Size"] = Mathf.Max(bounds.size.x, bounds.size.z) + 1;
        List<string> remaining = GetProblems(root);
        if (remaining.Count > 0) throw new FormatException(string.Join("; ", remaining));
        return true;
    }

    private static List<int> BuildBankOrder(JObject layout, JArray boxes)
    {
        var packets = new List<List<int>>();
        var assigned = new HashSet<int>();
        foreach (JObject group in A.Array(layout, "Groups"))
        {
            var packet = new List<int>();
            foreach (JToken index in A.Array(group, "BoxIndexes"))
            {
                int member = index.Value<int>();
                if (!assigned.Add(member)) throw new FormatException("Overlapping door groups.");
                packet.Add(member);
            }
            if (packet.Count > 0) packets.Add(packet);
        }
        for (int index = 0; index < boxes.Count; index++)
            if (assigned.Add(index)) packets.Add(new List<int> { index });

        var linkedBoxes = new Dictionary<int, int>();
        for (int index = 0; index < boxes.Count; index++)
        foreach (JObject cube in A.Array((JObject)boxes[index], "Cubes"))
        {
            int pairId = A.Int(cube, "LinkedPairId");
            if (pairId <= 0) continue;
            if (linkedBoxes.TryGetValue(pairId, out int first)) JoinLinkedPackets(packets, first, index);
            else linkedBoxes.Add(pairId, index);
        }

        int leftCount = (boxes.Count + 1) / 2;
        int partition = -1;
        for (int mask = 0; mask < (1 << packets.Count); mask++)
        {
            int count = 0;
            for (int index = 0; index < packets.Count; index++)
                if ((mask & (1 << index)) != 0) count += packets[index].Count;
            if (count != leftCount) continue;
            partition = mask;
            break;
        }
        if (partition < 0) throw new FormatException("Cannot keep door and linked neighbours together in two banks.");
        var order = new List<int>();
        for (int bank = 0; bank < 2; bank++)
        for (int index = 0; index < packets.Count; index++)
        {
            bool left = (partition & (1 << index)) != 0;
            if (left == (bank == 0)) order.AddRange(packets[index]);
        }
        return order;
    }

    private static void JoinLinkedPackets(List<List<int>> packets, int first, int second)
    {
        List<int> firstPacket = null, secondPacket = null;
        foreach (List<int> packet in packets)
        {
            if (packet.Contains(first)) firstPacket = packet;
            if (packet.Contains(second)) secondPacket = packet;
        }
        if (firstPacket == null || secondPacket == null) throw new FormatException("Missing linked box packet.");
        if (firstPacket == secondPacket)
        {
            firstPacket.Remove(first);
            firstPacket.Remove(second);
            firstPacket.Add(first);
            firstPacket.Add(second);
            return;
        }
        firstPacket.Remove(first);
        firstPacket.Add(first);
        secondPacket.Remove(second);
        secondPacket.Insert(0, second);
        firstPacket.AddRange(secondPacket);
        packets.Remove(secondPacket);
    }
}
#endif
