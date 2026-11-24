using System;
using System.Collections.Generic;

public class RelicManager : IDisposable
{
    public event Action<Relic> OnRelicAddedEvent;
    public event Action<Relic> OnRelicRemovedEvent;

    private List<Relic> relics = new List<Relic>();
    private Player owner;

    public RelicManager(Player owner)
    {
        this.owner = owner;
    }

    public Relic AddRelic(string relicId, bool isRestoredFromSave = false, int restoredCounter = 0)
    {
        Relic relic = ModObjectFactory.CreateRelic(relicId, owner, isRestoredFromSave, restoredCounter);
        if (relic != null)
        {
            relics.Add(relic);
            
            // Player's event bus triggers (using RelicData for backward compatibility)
            PlayerEventDispatcher.DispatchAddRelic(owner.eventBus, relic.Data);
            
            OnRelicAddedEvent?.Invoke(relic);
            UnityEngine.Debug.Log($"Relic Added : {relic.Data.relicName}");
        }
        return relic;
    }

    public void RemoveRelic(string relicId)
    {
        Relic relic = relics.Find(r => r.Data != null && r.Data.relicName == relicId);
        if (relic != null)
        {
            relics.Remove(relic);
            PlayerEventDispatcher.DispatchRemoveRelic(owner.eventBus, relic.Data);
            relic.Dispose();
            OnRelicRemovedEvent?.Invoke(relic);
            UnityEngine.Debug.Log($"Relic Removed : {relic.Data.relicName}");
        }
    }

    public Relic GetRandomRelic()
    {
        if (relics.Count == 0) return null;
        return relics[UnityEngine.Random.Range(0, relics.Count)];
    }

    public Relic GetSpecificRelic(string relicId)
    {
        return relics.Find(r => r.Data != null && r.Data.relicName == relicId);
    }
    
    public IReadOnlyList<Relic> GetRelics()
    {
        return relics;
    }

    public void ClearRelics()
    {
        foreach (var relic in relics)
        {
            if (relic != null)
            {
                relic.Dispose();
                OnRelicRemovedEvent?.Invoke(relic);
            }
        }
        relics.Clear();
    }

    public void Dispose()
    {
        OnRelicAddedEvent = null;
        OnRelicRemovedEvent = null;
        ClearRelics();
    }
}
