using System;
using System.Collections.Generic;

[Serializable]
public class Interval
{
    public float a;
    public float b;
    public Interval(float a, float b) { this.a = a; this.b = b; }
}

[Serializable]
public class AudioStruct
{
    public List<List<int>> AudioId;
    public List<Interval> AudioIntervals;
    public AudioStruct(List<List<int>> audioId, List<Interval> interval)
    {
        AudioId = audioId;
        AudioIntervals = interval;
    }
}
