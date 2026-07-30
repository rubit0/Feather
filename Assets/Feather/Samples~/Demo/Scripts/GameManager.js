// GameManager.js — spawns coins and tracks win state.
// Put this on an empty "GameManager" object in the scene.
class GameManager extends jsBehaviour {
  @Public
  @Header("Spawning")
  @Tooltip("How many coins to scatter when the game starts")
  @Range(1, 40)
  coinCount = 12;

  @Public
  @Assets
  @Required
  @Tooltip("Prefab with a Coin JS behaviour + trigger collider")
  coinPrefab = GameObject;

  @Public
  @Range(2, 30)
  spawnRadius = 10.0;

  @Public
  @Tooltip("World-space Y used when dropping coins (match your ground height)")
  spawnHeight = -2.4;

  @Public
  @Header("UI")
  @Tooltip("Shown when every coin is collected")
  winPanel = GameObject;

  @Public
  winLabel = Text;

  @Public
  @Header("Audio")
  @Assets
  @Tooltip("Played once when every coin is collected")
  successSfx = AudioClip;

  Awake() {
    this.collectedCount = 0;
    this.won = false;
    if (this.winPanel) this.winPanel.SetActive(false);
  }

  Start() {
    this.SpawnCoins();
  }

  SpawnCoins() {
    if (!this.coinPrefab) {
      Unity.Debug.LogError("[GameManager] Assign coinPrefab in the Inspector.");
      return;
    }

    // Scatter coins in a circle around this GameObject (XZ plane)
    const origin = this.transform.position;
    for (let i = 0; i < this.coinCount; i++) {
      const angle = (i / this.coinCount) * Math.PI * 2 + Unity.Random.Range(-0.2, 0.2);
      const dist = Unity.Random.Range(this.spawnRadius * 0.35, this.spawnRadius);
      const pos = new Vector3(
        origin.x + Math.cos(angle) * dist,
        this.spawnHeight,
        origin.z + Math.sin(angle) * dist,
      );

      // Same as Object.Instantiate in C#
      Unity.Object.Instantiate(this.coinPrefab, pos, Quaternion.identity);
    }

    Unity.Debug.Log(`[GameManager] Spawned ${this.coinCount} coins.`);
  }

  // Called by Coin.Collect() — keeps GameManager as the single source of truth for "win"
  OnCoinCollected(coin) {
    if (this.won) return;
    this.collectedCount++;

    if (this.collectedCount >= this.coinCount) this.Win();
  }

  Win() {
    this.won = true;
    if (this.winPanel) this.winPanel.SetActive(true);
    if (this.winLabel) {
      const secs = Unity.Time.timeSinceLevelLoad;
      this.winLabel.text = `You collected all coins in ${secs.toFixed(1)}s!`;
    }

    if (this.successSfx) {
      AudioSource.PlayClipAtPoint(this.successSfx, this.transform.position);
    }

    // Unlock the mouse so the player can read the win UI
    Unity.Cursor.lockState = Unity.CursorLockMode.None;
    Unity.Cursor.visible = true;

    Unity.Debug.Log("[GameManager] Win!");
  }

  Update() {
    // Press R to reload the active scene (simple demo restart)
    if (Unity.Input.GetKeyDown(Unity.KeyCode.R)) {
      const SceneManager = importNamespace("UnityEngine.SceneManagement").SceneManager;
      SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
  }
}
