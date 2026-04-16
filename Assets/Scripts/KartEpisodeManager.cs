using System.Collections.Generic;
using UnityEngine;

public class KartEpisodeManager : MonoBehaviour
{
    private readonly List<KartAgent> agents = new List<KartAgent>();

    private int successCount;
    private int failCount;
    private int finishedCount;

    public void RegisterAgent(KartAgent agent)
    {
        if (agent == null) return;
        if (!agents.Contains(agent))
            agents.Add(agent);
    }

    public void ReportResult(bool success)
    {
        if (success) successCount++;
        else failCount++;

        finishedCount++;

        if (finishedCount >= agents.Count && agents.Count > 0)
        {
            Debug.Log("Episode complete | Success: " + successCount + " | Fail: " + failCount);

            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i] != null)
                    agents[i].EndEpisode();
            }

            successCount = 0;
            failCount = 0;
            finishedCount = 0;
        }
    }
}