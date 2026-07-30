// TimerUI.js — elapsed time in the top-right corner.
// Attach to PanelTime under the Canvas.
class TimerUI extends jsBehaviour {
  @Public
  timeLabel = Text;

  @Public
  @Tooltip("Freeze the clock when the GameManager reports a win")
  stopOnWin = true;

  Awake() {
    this._stopped = false;
    this._finalTime = 0;
  }

  Start() {
    if (!this.timeLabel) {
      this.timeLabel = this.gameObject.GetComponentInChildren(Text);
    }
  }

  Update() {
    if (this.stopOnWin && !this._stopped) {
      const gm = Feather.findBehaviour(GameManager);
      if (gm && gm.won) {
        this._stopped = true;
        this._finalTime = Unity.Time.timeSinceLevelLoad;
      }
    }

    const t = this._stopped ? this._finalTime : Unity.Time.timeSinceLevelLoad;
    const minutes = Math.floor(t / 60);
    const seconds = Math.floor(t % 60);
    const padded = seconds < 10 ? "0" + seconds : String(seconds);

    if (this.timeLabel) this.timeLabel.text = `${minutes}:${padded}`;
  }
}
