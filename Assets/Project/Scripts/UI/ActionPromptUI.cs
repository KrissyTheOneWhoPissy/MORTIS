using UnityEngine;
using TMPro;

public class ActionPromptUI : MonoBehaviour
{
    public static ActionPromptUI Instance { get; private set; }

    [SerializeField] private TMP_Text promptText;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeSpeed = 12f;

    string _targetText;
    bool _show;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
    }

    void Update()
    {
        if (!group) return;
        float target = _show ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
        group.blocksRaycasts = _show;
        group.interactable = _show;
    }

    public void Show(string text)
    {
        _targetText = text;
        if (promptText) promptText.text = _targetText;
        _show = true;
    }

    public void Hide()
    {
        _show = false;
    }

    public void HideImmediate()
    {
        _show = false;
        if (group) group.alpha = 0f;
    }
}
