using UnityEngine;
using System.Collections;

public class MysteryBox : MonoBehaviour
{
    [Header("Settings")]
    public float respawnTime = 8f;          // How long before the box reappears
    public float shrinkDuration = 0.3f;     // How fast it shrinks

    private Vector3 originalScale;          // Store the original size
    private bool isActive = true;           // Can it be triggered?

    void Start()
    {
        originalScale = transform.localScale;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;              // Already used, ignore
        if (other.CompareTag("KART"))
        {
            StartCoroutine(ShrinkAndRespawn());
        }
    }

    public IEnumerator ShrinkAndRespawn()
    {
        isActive = false;

        // Shrink animation
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        while (elapsed < shrinkDuration)
        {
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / shrinkDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = Vector3.zero; // Fully invisible

        // Wait for respawn time
        yield return new WaitForSeconds(respawnTime);

        // Reappear instantly
        transform.localScale = originalScale;
        isActive = true;
    }
}