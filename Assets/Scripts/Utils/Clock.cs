using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Clock {

    #region Attributes

    /*
     * @brief Event that is called when the timer is over.
     */
    public event Action OnTimerOver;

    /*
     * @brief Timer is a float that represents the countdown timer.
     */
    private float _Timer = 0.0f;

    /*
     * @brief IsStarted is a boolean that indicates if the countdown is running.
     * - True  : The countdown is running.
     * - False : The countdown is not running.
     */
    private bool _IsStarted = false;

    #endregion

    #region Constructor

    public Clock(float timer)
    {
        _Timer = timer;
    }

    #endregion

    #region Methods

    /*
     * @brief Function that updates the clock.
     */
    public void Update()
    {
        // -- Update the timer if the countdown is running (If not, call the Start() function)
        if (_IsStarted) {
            _Timer -= Time.deltaTime; // -- Decrease the timer

            if (_Timer <= 0) {
                OnTimerOver?.Invoke(); // -- Callback event when the timer is over

                Stop(); // -- Stop the clock
            }
        }
    }

    /*
     * @brief Function that starts the clock.
     */
    public void Start()
    {
        _IsStarted = true;
    }

    /*
     * @brief Function that stops the clock.
     */
    public void Stop()
    {
        _IsStarted = false;
    }

    #endregion

    #region Getters & Setters

    public bool IsStarted
    {
        get => _IsStarted;
    }

    public void SetTimer(float timer)
    {
        _Timer = timer;
    }

    public float GetTimer()
    {
        return _Timer;
    }

    public int GetTimerInInt()
    {
        return (int)_Timer;
    }

    #endregion
}
