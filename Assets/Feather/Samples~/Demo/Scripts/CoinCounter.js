// CoinCounter.js — HUD that shows collected / total coins.
// Attach to the PanelCounter object under the Canvas.
class CoinCounter extends jsBehaviour {
  @Public
  @Tooltip("Label for how many coins the player has picked up")
  collectedLabel = Text;

  @Public
  @Tooltip("Label for the total number of coins (from GameManager.coinCount)")
  totalLabel = Text;

  Start() {
    // Convenience: wire labels from children if you left the slots empty
    if (!this.collectedLabel || !this.totalLabel) {
      const texts = this.gameObject.GetComponentsInChildren(Text);
      if (!this.collectedLabel && texts.length > 0) this.collectedLabel = texts[0];
      if (!this.totalLabel && texts.length > 1) this.totalLabel = texts[texts.length - 1];
    }
  }

  Update() {
    const gm = Feather.findBehaviour(GameManager);
    const collected = gm ? gm.collectedCount : 0;
    const total = gm ? gm.coinCount : 0;

    if (this.collectedLabel) this.collectedLabel.text = String(collected);
    if (this.totalLabel) this.totalLabel.text = String(total);
  }
}
