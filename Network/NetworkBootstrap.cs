using UnityEngine;

public sealed class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkLoginView loginUI;

    private void Start()
    {
        NetworkStateMachine.Instance.OnStateChanged += HandleStateChange;
        NetworkStateMachine.Instance.OnServerTimeFetched += HandleServerTime;
    }

    private void OnDestroy()
    {
        if (NetworkStateMachine.Instance == null)
            return;

        NetworkStateMachine.Instance.OnStateChanged -= HandleStateChange;
        NetworkStateMachine.Instance.OnServerTimeFetched -= HandleServerTime;
    }

    private void HandleStateChange(NetworkState state)
    {
        Debug.Log($"[Bootstrap] Network State: {state}");
    }

    private void HandleServerTime(ServerClockSnapshot snapshot)
    {
        Debug.Log($"[Bootstrap] Local server time: {snapshot.LocalNow}, Event start: {snapshot.StartLocal}, Event end: {snapshot.EndLocal}");
    }
}
