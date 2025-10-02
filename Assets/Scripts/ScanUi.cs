using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScanUi : MonoBehaviour
{
    [SerializeField] private Button _applyButton;
    [SerializeField] private TMP_Text _hint;
    
    private PoseScanerManager _poseScanerManager;
    
    void Start()
    {
        _hint.gameObject.SetActive(false);
        _applyButton.gameObject.SetActive(false);
        _poseScanerManager = FindObjectOfType<PoseScanerManager>();
        _applyButton.onClick.AddListener(Scan);

        _poseScanerManager.OnSetupTrackingCompleted += data =>
        {
            _hint.gameObject.SetActive(false);
            _applyButton.gameObject.SetActive(false);
        };
        
        _poseScanerManager.OnMarkDetected += (data) =>
        {
            _hint.gameObject.SetActive(true);
            _applyButton.gameObject.SetActive(true);
            
            if (data.progress == 0)
            {
                _hint.text = "Сканируйте метку так чтобы рамка совпадала с ней";
            }
            else
            {
                _hint.text = "Сканируйте метки \n" + data.progress + "/" + data.setup.trackers.Count;
            }
        };
    }

    private void Scan()
    {
        _applyButton.gameObject.SetActive(false);
        _poseScanerManager.ApplyPosition();
    }
}
