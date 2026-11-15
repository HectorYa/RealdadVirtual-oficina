using System;
using System.Collections;
using UnityEngine;

/// Detecta la cercanía a la silla, y anima sentarse / levantarse.
public class SeatInteraction : MonoBehaviour
{
    public Transform seatPoint;                 // yaw del seatPoint = hacia donde mira al sentarse
    public PlayerController player;
    public float sitRadius = 1.9f;
    public Vector3 seatedCamLocal = new Vector3(0f, 1.12f, 0.55f);
    public float standingEyeHeight = 1.65f;

    public bool Seated { get; private set; }
    public bool NearSeat { get; private set; }
    public bool CanStand = true;
    public event Action OnSat, OnStood;
    bool busy;

    void Update()
    {
        if (busy) return;
        Vector3 d = player.transform.position - seatPoint.position; d.y = 0;
        NearSeat = !Seated && d.magnitude < sitRadius;
        if (MobileInput.InteractDown)
        {
            if (NearSeat) Sit(); else if (Seated && CanStand) Stand();
        }
    }

    public void Sit() { if (!busy && !Seated && NearSeat) StartCoroutine(SitRoutine()); }

    /// Sienta al jugador de inmediato (reinicio rápido desde el reporte final).
    public void SitInstant()
    {
        if (Seated) return;
        player.controlsEnabled = false;
        player.GetComponent<CharacterController>().enabled = false;
        player.transform.SetPositionAndRotation(seatPoint.position, Quaternion.Euler(0f, seatPoint.eulerAngles.y, 0f));
        player.cam.localPosition = seatedCamLocal; player.cam.localRotation = Quaternion.identity;
        Seated = true; NearSeat = false;
        OnSat?.Invoke();
    }
    public void Stand() { if (!busy && Seated) StartCoroutine(StandRoutine()); }

    IEnumerator SitRoutine()
    {
        busy = true; NearSeat = false;
        player.controlsEnabled = false;
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
        yield return Move(seatPoint.position, seatPoint.eulerAngles.y, seatedCamLocal, true);
        Seated = true; busy = false;
        OnSat?.Invoke();
    }

    IEnumerator StandRoutine()
    {
        busy = true;
        Vector3 outPos = seatPoint.position - seatPoint.forward * 1.0f;
        yield return Move(outPos, seatPoint.eulerAngles.y + 180f, new Vector3(0, standingEyeHeight, 0), false);
        player.GetComponent<CharacterController>().enabled = true;
        player.ResetLook(); player.controlsEnabled = true;
        Seated = false; busy = false;
        OnStood?.Invoke();
    }

    IEnumerator Move(Vector3 pos, float yaw, Vector3 camLocal, bool levelCamera)
    {
        Transform p = player.transform, c = player.cam;
        Vector3 p0 = p.position, c0 = c.localPosition; float y0 = p.eulerAngles.y;
        Quaternion r0 = c.localRotation;
        const float dur = 1.4f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / dur);
            p.position = Vector3.Lerp(p0, pos, k);
            p.rotation = Quaternion.Euler(0, Mathf.LerpAngle(y0, yaw, k), 0);
            c.localPosition = Vector3.Lerp(c0, camLocal, k);
            c.localRotation = Quaternion.Slerp(r0, Quaternion.identity, k);
            yield return null;
        }
        p.position = pos; p.rotation = Quaternion.Euler(0, yaw, 0); c.localPosition = camLocal; c.localRotation = Quaternion.identity;
    }
}
