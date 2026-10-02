using UnityEngine;

public class MenuController : PersistentSingleton<MenuController>
{
    [Header("Input Manager")]
    [SerializeField]
    private InputReader _Input;

    [Header("Menu Panels")]
    [SerializeField]
    private GameObject _PausePanels;
    [SerializeField]
    private GameObject _SettingsPanel;

    private bool isOpen = false;

    protected override void OnSingletonAwake()
    {
        if (_Input)
        {
            _Input.MenuEvent += OnMenuPerformed;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_Input)
        {
            _Input.MenuEvent -= OnMenuPerformed;
        }
    }

    private void OnMenuPerformed()
    {
        if (isOpen)
            CloseMenu();
        else
            OpenMenu();

        isOpen = !isOpen;
    }

    private void OpenMenu()
    {
        _PausePanels.SetActive(true);
        _SettingsPanel.SetActive(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void CloseMenu()
    {
        _PausePanels.SetActive(false);
        _SettingsPanel.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    public void NaviageSettings()
    {
        _PausePanels.SetActive(false);
        _SettingsPanel.SetActive(true);
    }

    public void GoBack()
    {
        if (_PausePanels.activeSelf)
        {
            CloseMenu();
            isOpen = false;
        }
        else
        {
            if (_SettingsPanel.activeSelf)
            {
                _SettingsPanel.SetActive(false);
                _PausePanels.SetActive(true);
            }
        }
    }
}