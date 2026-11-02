"""Sound effect recipes. Each function returns a rendered Track."""

from synth import Track, note_freq, saw, square, triangle


def pulse(duty):
    return lambda p: square(p, duty)


def select():
    """Picking up a unit: two quick rising blips."""
    t = Track(0.25)
    t.add_tone(0, 0.05, note_freq("E5"), pulse(0.5), 0.2, release=0.02)
    t.add_tone(0.06, 0.09, note_freq("B5"), pulse(0.5), 0.2, release=0.05)
    return t


def move():
    """Two soft footsteps on stone."""
    t = Track(0.4)
    for at, seed in [(0.0, 11), (0.17, 12)]:
        t.add_noise(at, 0.08, 0.5, smoothing=0.85, decay_power=1.8, seed=seed)
        t.add_tone(at, 0.05, 110, triangle, 0.35, freq_end=70, release=0.03)
    return t


def slash():
    """A melee strike: a bright swish into a heavy thud."""
    t = Track(0.45)
    t.add_noise(0, 0.12, 0.45, smoothing=0.1, decay_power=1.2, seed=21)
    t.add_tone(0, 0.1, 900, saw, 0.12, freq_end=300, release=0.03)
    t.add_noise(0.08, 0.22, 0.7, smoothing=0.75, decay_power=1.8, seed=22)
    t.add_tone(0.08, 0.16, 150, triangle, 0.7, freq_end=50, release=0.08)
    return t


def bolt():
    """A ranged spell: a rising shimmer that lands with a crackle."""
    t = Track(0.6)
    t.add_tone(0, 0.22, 300, pulse(0.125), 0.16, freq_end=1400, release=0.04, vibrato=0.02)
    t.add_noise(0.22, 0.3, 0.55, smoothing=0.45, decay_power=1.6, seed=31)
    t.add_tone(0.22, 0.15, 220, triangle, 0.5, freq_end=70, release=0.08)
    return t


def defeat_unit():
    """A unit falls: a sinking tone over a low rumble."""
    t = Track(0.9)
    t.add_tone(0, 0.6, note_freq("A4"), pulse(0.25), 0.16, freq_end=note_freq("A2"),
               release=0.15, vibrato=0.01)
    t.add_noise(0.05, 0.5, 0.5, smoothing=0.9, decay_power=1.5, seed=41)
    t.add_tone(0.05, 0.3, 90, triangle, 0.6, freq_end=40, release=0.1)
    return t


def promote():
    """A pawn becomes a war dog: a quick rising arpeggio."""
    t = Track(0.9)
    for i, name in enumerate(["A4", "C#5", "E5", "A5", "C#6"]):
        t.add_tone(i * 0.07, 0.12 if i < 4 else 0.4, note_freq(name), pulse(0.25), 0.2,
                   release=0.08, vibrato=0.006 if i == 4 else 0.0)
    return t


def horn(notes, base):
    t = Track(1.0)
    at = 0.0
    for name, length in notes:
        t.add_tone(at, length, note_freq(name), saw, 0.18, attack=0.03, decay=0.08, sustain=0.7,
                   release=0.12, vibrato=0.004)
        t.add_tone(at, length, note_freq(name) / 2, triangle, 0.3, attack=0.02, release=0.12)
        at += length + 0.02
    t.add_tone(0, at, note_freq(base), triangle, 0.25, release=0.1)
    return t


def turn_player():
    """A bright horn call when the player's turn starts."""
    return horn([("D4", 0.12), ("A4", 0.35)], "D2")


def turn_enemy():
    """A darker horn call for the enemy turn."""
    return horn([("A3", 0.12), ("F3", 0.35)], "D2")


def victory():
    t = Track(2.0)
    lead = pulse(0.25)
    for name, at, length in [("D5", 0.0, 0.12), ("F#5", 0.14, 0.12), ("A5", 0.28, 0.12),
                             ("D6", 0.42, 0.9)]:
        t.add_tone(at, length, note_freq(name), lead, 0.24, decay=0.06, sustain=0.65,
                   release=0.15, vibrato=0.006 if length > 0.5 else 0.0)
    for name in ["F#5", "A5"]:
        t.add_tone(0.42, 0.9, note_freq(name), pulse(0.125), 0.08, release=0.4)
    t.add_tone(0.42, 0.9, note_freq("D3"), triangle, 0.45, release=0.3)
    return t


def defeat():
    t = Track(2.0)
    for name, at, length in [("A4", 0.0, 0.25), ("F4", 0.3, 0.25), ("D4", 0.6, 0.25),
                             ("C#4", 0.9, 0.8)]:
        t.add_tone(at, length, note_freq(name), triangle, 0.35, release=0.2, vibrato=0.006)
    t.add_tone(0.9, 0.8, note_freq("A2"), triangle, 0.4, release=0.3)
    return t


def click():
    t = Track(0.08)
    t.add_tone(0, 0.025, 1300, pulse(0.5), 0.2, freq_end=900, release=0.02)
    return t


def invalid():
    """A short low buzz for an action that is not allowed."""
    t = Track(0.2)
    t.add_tone(0, 0.12, 140, pulse(0.5), 0.15, release=0.04)
    return t


EFFECTS = {
    "select.ogg": select,
    "move.ogg": move,
    "slash.ogg": slash,
    "bolt.ogg": bolt,
    "unit_down.ogg": defeat_unit,
    "promote.ogg": promote,
    "turn_player.ogg": turn_player,
    "turn_enemy.ogg": turn_enemy,
    "victory.ogg": victory,
    "defeat.ogg": defeat,
    "click.ogg": click,
    "invalid.ogg": invalid,
}
