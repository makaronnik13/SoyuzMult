using UnityEngine;
using System;
using System.Collections.Generic;
using WebARFoundation;

[Serializable]
public class TrackingSetupData
{
    public List<ImageTracker> trackers;
    public GameObject model;
}
