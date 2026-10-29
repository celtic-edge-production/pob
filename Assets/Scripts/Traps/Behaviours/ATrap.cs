using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class ATrap : NetworkBehaviour
{
    [Flags]
    public enum TrapState
    {
        None = 0,
        Animate = 1 << 0,
        Collide = 1 << 1
    }

    [SerializeField] protected TrapState _state;
    [SerializeField] protected BoxCollider2D _boxColliderOverMouse;
    [SerializeField] protected AudioClip _destroyAudioClip;

    // Former per-trap fields: the attributes keep the clips already assigned in the prefabs.
    [Tooltip("Played on every peer by PlayActionSound(): hit, shot, fire...")]
    [FormerlySerializedAs("_AudioClip"), FormerlySerializedAs("_audioClip")]
    [SerializeField] protected AudioClip _actionSound;

    protected BoxCollider2D _boxCollider;
    protected TrapState _previousState;
    protected Animator _animator;

    private Vector3 _baseScale;

    // Server only: the cell the trap was placed on (its top-left cell).
    [NonSerialized] public Vector3Int cellPosition;
    // Server only: the player who chose the trap (meaningful from TrapStage.Chosen on).
    [NonSerialized] public ulong ChosenBy;
    // Set by each trap in Awake: its shape before any rotation.
    protected int[,] matrix;
    private TrapShape _baseShape;
    private TrapShape BaseShape => _baseShape ??= new TrapShape(matrix);

    // The orientation last shown by ShowAt (the placing player's preview, then the placed trap).
    public TrapOrientation Orientation { get; private set; } = TrapOrientation.Default;
    public TrapShape Shape => GetShape(Orientation);
    public bool isBombTrap = false;

    // Server-written; every peer reacts to it (selection in the chest, phase 2 activation).
    public NetworkVariable<TrapStage> Stage = new NetworkVariable<TrapStage>(TrapStage.InChest);
    public NetworkVariable<int> trapId = new NetworkVariable<int>();
    // Server-written during placement: false while the preview sits where the trap cannot be placed.
    public NetworkVariable<bool> isPreviewValid = new NetworkVariable<bool>(true);

    [Tooltip("Multiplied with the sprite colours while the preview is on a cell where the trap cannot be placed.")]
    [SerializeField] private Color _invalidPreviewTint = new Color(1f, 0.35f, 0.35f, 0.6f);
    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;

    // True while the trap is live (phase 2): behaviours only run then.
    protected bool IsActive => (_state & TrapState.Collide) == TrapState.Collide;

    // Set on every peer just before the end-of-game cleanup, so the traps do not all play their destroy sound at once.
    public static bool MuteDestroySound { get; set; }

    protected virtual void Awake()
    {
        _boxCollider = GetComponent<BoxCollider2D>();
        _animator = GetComponent<Animator>();
        _previousState = _state;
        _animator.enabled = true;
        _baseScale = transform.localScale;

        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseColors[i] = _renderers[i].color;
    }

    protected virtual void Start()
    {
    }

    // Server only. Refuses the moves TrapStageRules does not allow (skipping a step, going back).
    public bool TrySetStage(TrapStage next)
    {
        if (!IsServer || !TrapStageRules.CanMove(Stage.Value, next))
            return false;

        Stage.Value = next;
        return true;
    }

    // Every peer: collisions and animations are only on while the trap is active (phase 2).
    private void OnStageChanged(TrapStage previous, TrapStage current)
    {
        if (current == TrapStage.Active)
            _state |= TrapState.Collide | TrapState.Animate;
        else if (previous == TrapStage.Active)
            _state &= ~(TrapState.Collide | TrapState.Animate);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // A new trap means a new game (or a new placement phase): destroy sounds are allowed again.
        MuteDestroySound = false;

        isPreviewValid.OnValueChanged += OnPreviewValidityChanged;
        ApplyPreviewTint(isPreviewValid.Value);

        Stage.OnValueChanged += OnStageChanged;
        // A peer joining while the trap is already live gets the right state at once.
        if (Stage.Value == TrapStage.Active)
            OnStageChanged(TrapStage.Placed, TrapStage.Active);
    }

    public override void OnNetworkDespawn()
    {
        // Played locally on every peer: an RPC can no longer be sent once despawned.
        // Skipped during the end-of-game cleanup (TrapPlacementManager.DespawnAllTraps).
        if (_destroyAudioClip != null && !MuteDestroySound)
            AudioSource.PlayClipAtPoint(_destroyAudioClip, transform.position);

        isPreviewValid.OnValueChanged -= OnPreviewValidityChanged;
        Stage.OnValueChanged -= OnStageChanged;

        DestroyTrap();
        base.OnNetworkDespawn();
    }

    protected virtual void DestroyTrap()
    {
    }

    private void OnPreviewValidityChanged(bool previous, bool valid)
    {
        ApplyPreviewTint(valid);
    }

    // Every peer: the preview turns red where the trap cannot be placed.
    private void ApplyPreviewTint(bool valid)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].color = valid ? _baseColors[i] : _baseColors[i] * _invalidPreviewTint;
        }
    }

    protected virtual void Update()
    {
        // Applied to every trap: no animation and no collision outside phase 2.
        ApplyStateToComponents();

    }

    // Server only, right after the trap is placed. Nothing by default; the bomb trap explodes.
    public virtual void OnPlaced()
    {
    }

    // Chest selection: the cursor's click (TilemapHover) asks whether it is on this trap.
    public bool IsUnderPoint(Vector2 worldPoint)
    {
        return _boxColliderOverMouse != null && _boxColliderOverMouse.OverlapPoint(worldPoint);
    }

    public TrapShape GetShape(TrapOrientation orientation)
    {
        return orientation.Apply(BaseShape);
    }

    // Puts the trap on this cell with this orientation, in one step. Run by whoever moves the trap:
    // the placing player while he owns it (its owner-authority NetworkTransform replicates it), the server once placed.
    // The sprite pivot must be centred: the transform goes to the centre of the oriented shape.
    public void ShowAt(Vector3Int cell, TrapOrientation orientation)
    {
        Orientation = orientation;

        transform.SetPositionAndRotation(
            cell + GetShape(orientation).GetPreviewOffset(),
            Quaternion.Euler(0f, 0f, orientation.EulerZ));

        Vector3 scale = _baseScale;
        scale.x = Mathf.Abs(scale.x) * orientation.ScaleXSign;
        transform.localScale = scale;
    }

    protected virtual void FixedUpdate()
    {
    }

    private void ApplyStateToComponents()
    {
        if (_previousState == _state)
            return;

        bool animate = (_state & TrapState.Animate) == TrapState.Animate;

        _animator.enabled = animate;
        _boxCollider.enabled = (_state & TrapState.Collide) == TrapState.Collide;
        _previousState = _state;

        if (animate)
            OnAnimatorEnabled();
    }

    // Every peer, each time the trap comes alive again (start of every phase 2). Re-enabling an
    // Animator puts every parameter back to its default value: a trap that sets parameters in
    // Awake sets them again here.
    protected virtual void OnAnimatorEnabled()
    {
    }

    // Server only: plays _actionSound at the trap position on every peer.
    protected void PlayActionSound()
    {
        if (IsServer && _actionSound != null)
            PlayActionSoundClientRpc(transform.position);
    }

    // Server only: a player was hit by this trap. The hit is detected on the player's own
    // machine (PlayerCharacter.TrapPunch), where his physics runs, then reported here.
    // Overridden by the traps that play a sound on hit (Spikes, Shuriken).
    public virtual void OnPlayerHit()
    {
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayActionSoundClientRpc(Vector3 position)
    {
        if (_actionSound != null)
            AudioSource.PlayClipAtPoint(_actionSound, position);
    }
}
