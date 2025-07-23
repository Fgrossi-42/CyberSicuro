using UnityEngine;
using UnityEngine.UI;
using TMPro; // For TextMeshProUGUI
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;


[System.Serializable]
public class PasswordEntry
{
    public string passwordLabel; // e.g., "12345", "letmein"
    public int securityValue;    // e.g., 1–5
}

public class PasswordSortingGame : MonoBehaviour
{
    [Header("Password Data")]
    public List<PasswordEntry> allPasswords = new List<PasswordEntry>(); // Assign 5 in Inspector with label and value
    private Vector2 pointerOffset;

    [Header("UI References")]
    public GameObject[] passwordHolders;            // Parent objects with Image and child TMP
    public Button checkButton;

    public GameObject resultPanel;
    
    public GameObject correctPanel;
    public TextMeshProUGUI resultText;
    public GameObject wrongImage; // Assign this in the Inspector


    private TextMeshProUGUI[] passwordTexts;
    private List<PasswordEntry> shuffledPasswords = new List<PasswordEntry>();

    // For drag logic
    private GameObject draggedHolder;
    private RectTransform draggedRectTransform;
    private Vector2 originalPosition;

    void Start()
    {
        if (passwordHolders.Length != 5 || allPasswords.Count != 5)
        {
            Debug.LogError("Assign all 5 holders and 5 passwords in the Inspector.");
            return;
        }

        passwordTexts = new TextMeshProUGUI[5];
        for (int i = 0; i < passwordHolders.Length; i++)
        {
            passwordTexts[i] = passwordHolders[i].GetComponentInChildren<TextMeshProUGUI>();
            if (passwordTexts[i] == null) Debug.LogError($"Holder {i} needs a TextMeshProUGUI child.");
        }

        ShufflePasswords();
        UpdatePasswordTexts();

        foreach (var holder in passwordHolders)
            AddDragEvents(holder);

        checkButton.onClick.AddListener(CheckOrder);
    }

    // Shuffle entries for random appearance
    void ShufflePasswords()
    {
        shuffledPasswords = new List<PasswordEntry>(allPasswords);
        // Fisher-Yates shuffle
        for (int i = shuffledPasswords.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = shuffledPasswords[i];
            shuffledPasswords[i] = shuffledPasswords[j];
            shuffledPasswords[j] = temp;
        }
    }

    void UpdatePasswordTexts()
    {
        for (int i = 0; i < passwordTexts.Length; i++)
            passwordTexts[i].text = shuffledPasswords[i].passwordLabel;
    }

    void AddDragEvents(GameObject holder)
    {
        EventTrigger trigger = holder.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = holder.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDrag.callback.AddListener((data) => OnBeginDrag(holder));
        trigger.triggers.Add(beginDrag);

        var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener((data) => OnDrag(holder, (PointerEventData)data));
        trigger.triggers.Add(drag);

        var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDrag.callback.AddListener((data) => OnEndDrag((PointerEventData)data));
        trigger.triggers.Add(endDrag);
    }

    void OnBeginDrag(GameObject holder)
    {
        draggedHolder = holder;
        draggedRectTransform = holder.GetComponent<RectTransform>();
        originalPosition = draggedRectTransform.anchoredPosition;

        // Calculate offset between pointer and rect center
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)draggedRectTransform.parent,
            Input.mousePosition, null, out var localPointerPosition);
        pointerOffset = draggedRectTransform.anchoredPosition - localPointerPosition;
    }

    void OnDrag(GameObject holder, PointerEventData eventData)
    {
        if (holder == draggedHolder)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)draggedRectTransform.parent,
                eventData.position, eventData.pressEventCamera, out var localPointerPosition);
            draggedRectTransform.anchoredPosition = localPointerPosition + pointerOffset;
        }
    }


    void OnEndDrag(PointerEventData data)
    {
        if (draggedHolder == null) return;

        GameObject dropTarget = null;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        foreach (var result in results)
        {
            if (result.gameObject != draggedHolder &&
                System.Array.IndexOf(passwordHolders, result.gameObject) >= 0)
            {
                dropTarget = result.gameObject;
                break;
            }
            // Support dropping on TMP child as well
            var parentGo = result.gameObject.transform.parent ? result.gameObject.transform.parent.gameObject : null;
            if (parentGo != null && parentGo != draggedHolder &&
                System.Array.IndexOf(passwordHolders, parentGo) >= 0)
            {
                dropTarget = parentGo;
                break;
            }
        }

        if (dropTarget != null)
            SwapHolders(draggedHolder, dropTarget);

        // Snap back to layout position
        draggedRectTransform.anchoredPosition = originalPosition;
        draggedHolder = null; draggedRectTransform = null;
    }

    void SwapHolders(GameObject holder1, GameObject holder2)
    {
        int i1 = System.Array.IndexOf(passwordHolders, holder1);
        int i2 = System.Array.IndexOf(passwordHolders, holder2);
        if (i1 < 0 || i2 < 0) return;

        // Swap the passwords in the displayed order
        var temp = shuffledPasswords[i1];
        shuffledPasswords[i1] = shuffledPasswords[i2];
        shuffledPasswords[i2] = temp;

        UpdatePasswordTexts();
    }

    void CheckOrder()
    {
        bool correct = true;
        for (int i = 0; i < shuffledPasswords.Count - 1; i++)
        {
            if (shuffledPasswords[i].securityValue >= shuffledPasswords[i + 1].securityValue)
            {
                correct = false;
                break;
            }
        }
        ShowResultScreen(correct);
    }

    void ShowResultScreen(bool isCorrect)
    {
        // Always hide both first to avoid overlap
        resultPanel.SetActive(false);
        wrongImage.SetActive(false);

        if (isCorrect)
        {
            correctPanel.SetActive(true);
            StartCoroutine(HideWrongFeedbackAfterSeconds(2.5f));
        }
        else
        {
            // Show the wrong feedback, set text, and display the image
            resultPanel.SetActive(true);
            resultText.text = "Wrong!";
            resultText.color = Color.red;
            wrongImage.SetActive(true);
            // Start automatic hide coroutine (e.g. after 2.5 seconds)
            StartCoroutine(HideWrongFeedbackAfterSeconds(2.5f));
        }
    }

    IEnumerator HideWrongFeedbackAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        resultPanel.SetActive(false);
        wrongImage.SetActive(false);
        correctPanel.SetActive(false);
    }
}
