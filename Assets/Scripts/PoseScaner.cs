using System;
using System.Collections.Generic;
using UnityEngine;
using WebARFoundation;

public class PoseScanner : MonoBehaviour
{
    [SerializeField] private GameObject _mark;
    
    private ImageTracker _tracker;
    private PoseScanerManager _scanerManager;
    private void Awake()
    {
        _scanerManager = FindObjectOfType<PoseScanerManager>();
        _tracker = GetComponent<ImageTracker>();
        _scanerManager.OnSetupTrackingCompleted += TrackingCompleted;
    }

    private void TrackingCompleted(TrackingSetupData obj)
    {
        _mark.SetActive(!_scanerManager.IsMarkScaned(_tracker));
    }

    private void OnEnable()
    {
        _scanerManager.MarkDetected(_tracker);
    }
        
}