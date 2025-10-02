using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WebARFoundation;

public class PoseScanerManager : MonoBehaviour
{
    [SerializeField] private List<TrackingSetupData> _setups;

    public Action<MarkFoundData> OnMarkDetected;
    public Action<TrackingSetupData> OnSetupTrackingCompleted;

    private readonly Dictionary<ImageTracker, Vector3> _scanedImages = new Dictionary<ImageTracker, Vector3>();
    private readonly Dictionary<TrackingSetupData, Dictionary<ImageTracker, Vector3>> _refLocalBySetup = new Dictionary<TrackingSetupData, Dictionary<ImageTracker, Vector3>>();

    private ImageTracker _currentImage;
    private TrackingSetupData _currentSetup;

    public void MarkDetected(ImageTracker imageTracker)
    {
        _currentImage = imageTracker;
        var setup = GetSetup(_currentImage);
        if (setup != _currentSetup)
        {
            if (_currentSetup != null) _currentSetup.model.gameObject.SetActive(false);
            _scanedImages.Clear();
            _currentSetup = setup;
            EnsureReferenceLocals(_currentSetup);
            OnMarkDetected?.Invoke(new MarkFoundData { setup = _currentSetup, progress = _scanedImages.Count });
        }
    }

    public void ApplyPosition()
    {
        if (_currentSetup == null || _currentImage == null) return;

        if (!_scanedImages.ContainsKey(_currentImage))
            _scanedImages.Add(_currentImage, _currentImage.transform.position);

        OnMarkDetected?.Invoke(new MarkFoundData { setup = _currentSetup, progress = _scanedImages.Count });

        if (_scanedImages.Count == _currentSetup.trackers.Count)
        {
            var rot = GetRotation(_currentSetup, _scanedImages);
            var pos = GetPosition(_currentSetup, _scanedImages, rot);
            OnSetupTrackingCompleted?.Invoke(_currentSetup);
            _currentSetup.model.SetActive(true);
            _currentSetup.model.transform.position = Camera.main.transform.position+Camera.main.transform.forward*2;
            
            Debug.LogError( _currentSetup.model.transform.position+" "+Camera.main.transform.position);
        }
    }

    private void EnsureReferenceLocals(TrackingSetupData setup)
    {
        if (setup == null || setup.model == null) return;
        if (_refLocalBySetup.ContainsKey(setup)) return;
        var map = new Dictionary<ImageTracker, Vector3>();
        foreach (var tr in setup.trackers)
            if (tr != null)
                map[tr] = setup.model.transform.InverseTransformPoint(tr.transform.position);
        _refLocalBySetup[setup] = map;
    }

    private Quaternion GetRotation(TrackingSetupData currentSetup, Dictionary<ImageTracker, Vector3> scanedImages)
    {
        if (currentSetup == null || currentSetup.model == null) return Quaternion.identity;
        if (!_refLocalBySetup.TryGetValue(currentSetup, out var refLocals)) return Quaternion.identity;

        var parent = currentSetup.model.transform.parent;
        var dst = new List<Vector3>();
        var src = new List<Vector3>();

        foreach (var kv in scanedImages)
        {
            if (!refLocals.TryGetValue(kv.Key, out var localRef)) continue;
            src.Add(localRef);
            var p = kv.Value;
            dst.Add(parent ? parent.InverseTransformPoint(p) : p);
        }

        if (src.Count < 2) return currentSetup.model.transform.rotation;

        var r = SolveRotationHorn(src, dst);
        return parent ? r * parent.rotation : r;
    }

    private Vector3 GetPosition(TrackingSetupData currentSetup, Dictionary<ImageTracker, Vector3> scanedImages, Quaternion withRotation)
    {
        if (currentSetup == null || currentSetup.model == null) return Vector3.zero;
        if (!_refLocalBySetup.TryGetValue(currentSetup, out var refLocals)) return Vector3.zero;

        var parent = currentSetup.model.transform.parent;
        var dst = new List<Vector3>();
        var src = new List<Vector3>();

        foreach (var kv in scanedImages)
        {
            if (!refLocals.TryGetValue(kv.Key, out var localRef)) continue;
            src.Add(localRef);
            var p = kv.Value;
            dst.Add(parent ? parent.InverseTransformPoint(p) : p);
        }

        if (src.Count == 0) return currentSetup.model.transform.position;

        var cSrc = Centroid(src);
        var cDst = Centroid(dst);

        if (parent)
        {
            var rLocal = Quaternion.Inverse(parent.rotation) * withRotation;
            var tLocal = cDst - rLocal * cSrc;
            return parent.TransformPoint(tLocal);
        }
        else
        {
            var t = cDst - withRotation * cSrc;
            return t;
        }
    }

    public TrackingSetupData GetSetup(ImageTracker imageTracker)
    {
        return _setups.FirstOrDefault(s => s.trackers.Contains(imageTracker));
    }

    private static Vector3 Centroid(List<Vector3> pts)
    {
        var c = Vector3.zero;
        for (int i = 0; i < pts.Count; i++) c += pts[i];
        return c / Mathf.Max(1, pts.Count);
    }

    private static Quaternion SolveRotationHorn(List<Vector3> srcLocal, List<Vector3> dstParent)
    {
        var cSrc = Centroid(srcLocal);
        var cDst = Centroid(dstParent);

        var Sxx = 0f; var Sxy = 0f; var Sxz = 0f;
        var Syx = 0f; var Syy = 0f; var Syz = 0f;
        var Szx = 0f; var Szy = 0f; var Szz = 0f;

        for (int i = 0; i < srcLocal.Count; i++)
        {
            var a = srcLocal[i] - cSrc;
            var b = dstParent[i] - cDst;
            Sxx += a.x * b.x; Sxy += a.x * b.y; Sxz += a.x * b.z;
            Syx += a.y * b.x; Syy += a.y * b.y; Syz += a.y * b.z;
            Szx += a.z * b.x; Szy += a.z * b.y; Szz += a.z * b.z;
        }

        var K = new float[4, 4];
        var trace = Sxx + Syy + Szz;

        K[0, 0] = trace;
        K[0, 1] = Szy - Syz;
        K[0, 2] = Sxz - Szx;
        K[0, 3] = Syx - Sxy;

        K[1, 0] = Szy - Syz;
        K[1, 1] = Sxx - Syy - Szz;
        K[1, 2] = Sxy + Syx;
        K[1, 3] = Sxz + Szx;

        K[2, 0] = Sxz - Szx;
        K[2, 1] = Sxy + Syx;
        K[2, 2] = -Sxx + Syy - Szz;
        K[2, 3] = Syz + Szy;

        K[3, 0] = Syx - Sxy;
        K[3, 1] = Sxz + Szx;
        K[3, 2] = Syz + Szy;
        K[3, 3] = -Sxx - Syy + Szz;

        var q = PowerIterationEigenvector(K, 64);
        var quat = new Quaternion(q[1], q[2], q[3], q[0]);
        return NormalizeSafe(quat);
    }

    private static float[] PowerIterationEigenvector(float[,] A, int iters)
    {
        var v = new float[4] { 1, 0, 0, 0 };
        for (int k = 0; k < iters; k++)
        {
            var w = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float s = 0f;
                for (int j = 0; j < 4; j++) s += A[i, j] * v[j];
                w[i] = s;
            }
            float n = Mathf.Sqrt(w[0] * w[0] + w[1] * w[1] + w[2] * w[2] + w[3] * w[3]);
            if (n < 1e-9f) break;
            v[0] = w[0] / n; v[1] = w[1] / n; v[2] = w[2] / n; v[3] = w[3] / n;
        }
        return v;
    }

    private static Quaternion NormalizeSafe(Quaternion q)
    {
        var n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (n < 1e-9f) return Quaternion.identity;
        return new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n);
    }

    public bool IsMarkScaned(ImageTracker tracker)
    {
        return _scanedImages.ContainsKey(tracker);
    }
}

public struct MarkFoundData
{
    public TrackingSetupData setup;
    public int progress;
}
