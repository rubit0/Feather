// Coin.js — pickup item with a trigger collider.
// Attach this to the CollectCoin prefab (or any coin mesh in the scene).
// Collect uses a generator coroutine for the shrink/spin out.
class Coin extends jsBehaviour {
  @Public
  @Header("Motion")
  @Tooltip("Degrees per second — classic collectible spin")
  spinSpeed = 90.0;

  @Public
  @Range(0, 1)
  bobHeight = 0.15;

  @Public
  @Range(0.5, 5)
  bobSpeed = 2.0;

  @Public
  @Header("Audio")
  @Assets
  @Tooltip("Played at the coin position when collected")
  collectSfx = AudioClip;

  Awake() {
    // Public flag so CoinCounter can tally collected coins without destroying them
    this.collected = false;
    this._baseY = this.transform.position.y;
    this._phase = Unity.Random.Range(0, Math.PI * 2);
  }

  Update() {
    if (this.collected) return;

    // Rotate around world up
    this.transform.Rotate(0, this.spinSpeed * Unity.Time.deltaTime, 0);

    // Gentle bob so coins read as "alive" in the level
    const y =
      this._baseY +
      Math.sin(Unity.Time.time * this.bobSpeed + this._phase) * this.bobHeight;
    const p = this.transform.position;
    this.transform.position = new Vector3(p.x, y, p.z);
  }

  // CharacterController walks into a trigger → OnTriggerEnter fires on this script
  OnTriggerEnter(other) {
    if (this.collected) return;

    // Only the player should collect (tagged or named — keep the demo simple)
    if (other.gameObject.name.indexOf("FPSPlayer") < 0 && other.tag !== "Player")
      return;

    this.Collect();
  }

  Collect() {
    this.collected = true;

    if (this.collectSfx) {
      AudioSource.PlayClipAtPoint(this.collectSfx, this.transform.position);
    }

    // Stop pickup immediately; mesh hides after the collect coroutine
    const col = this.gameObject.GetComponent(Collider);
    if (col) col.enabled = false;

    const gm = Feather.findBehaviour(GameManager);
    if (gm && gm.OnCoinCollected) gm.OnCoinCollected(this);

    // Generator coroutine — yield null = next frame, yield number = seconds
    this.startCoroutine(this.collectAnim());
  }

  // Demo of Feather coroutines: spin + shrink over ~0.35s, then hide the mesh
  *collectAnim() {
    const start = this.transform.localScale;
    const duration = 0.35;
    let t = 0;

    while (t < duration) {
      t += Unity.Time.deltaTime;
      const u = Math.min(t / duration, 1);
      const s = 1 - u * u; // ease-out
      this.transform.localScale = new Vector3(start.x * s, start.y * s, start.z * s);
      this.transform.Rotate(0, 720 * Unity.Time.deltaTime, 0);
      yield null;
    }

    const renderer = this.gameObject.GetComponent(MeshRenderer);
    if (renderer) renderer.enabled = false;

    Unity.Debug.Log("[Coin] Collected!");
  }
}
