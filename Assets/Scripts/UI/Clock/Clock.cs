using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ClockUI : MonoBehaviour {

    #region Attributes

    /*
    * @brief ClockText is the text that will be displayed on the screen.
    * It will show the time left before the timer is over.
    */
    [SerializeField]
    private TMP_Text _ClockText;

    /*
    * @brief Clock that is linked to the UI.
    */
    public Clock Clock;

    #endregion

    #region Methods

    public void OnEnable()
    {
        Clock = new Clock(0);
    }

    public void OnDisable()
    {
        Clock = null;
    }

    public void Update()
    {
        if (_ClockText != null && Clock != null)
            _ClockText.text = Clock.GetTimerInInt().ToString();
    }

    public void SetTime(float time)
    {
        if (Clock != null)
            Clock.SetTimer(time);
    }

    #endregion
}
