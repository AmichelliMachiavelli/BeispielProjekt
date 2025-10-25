using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CalendarDoorConfigEditorView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dateTxt;
    [SerializeField] private TextMeshProUGUI dayTxt;
    [SerializeField] private TMP_InputField personalNoteInput;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private Button saveButton;

    public event Action<DoorConfigurationData> OnConfigSaved;
    public event Action OnBackPressed;

    private DoorConfigurationData _current;

    private void Awake()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveConfig);
        
        if (typeDropdown != null)
        {
            typeDropdown.ClearOptions();

            var types = Enum.GetValues(typeof(DoorGameType));
            foreach (var t in types)
                typeDropdown.options.Add(new TMP_Dropdown.OptionData(ObjectNames.NicifyVariableName(t.ToString())));

            typeDropdown.value = 0;
            typeDropdown.RefreshShownValue();

            Debug.Log($"[CalendarDoorConfigEditorView] Dropdown initialized with {typeDropdown.options.Count} DoorTypes.");
        }
    }

    public void Open(CalendarDescription calendar, DoorConfigurationData data)
    {
        _current = data;

        if(CalendarTimeUtility.TryGetDoorDate(calendar, data.Index, out var date))
        {
            dateTxt.text = date.ToString("dd.MM.yyyy");
        }
        dayTxt.text = "#" + data.Index.ToString();
        personalNoteInput.text = data.PersonalNote;
        typeDropdown.value = (int)data.GameType;
    }

    private void SaveConfig()
    {
        _current.PersonalNote = personalNoteInput.text;
        _current.GameType = (DoorGameType)typeDropdown.value;

        OnConfigSaved?.Invoke(_current);
        OnBackPressed?.Invoke();
        Debug.Log($"[CalendarDoorConfigEditorView] Saved config for door #{_current.Index}");
    }
}
