#!/usr/bin/env bash
# Converts the original audio in AudioSource/ (never modified) into game-ready OGG Vorbis under Assets/_Game/Audio.
# Re-run any time: ./Tools/process_audio.sh   (needs ffmpeg). Cut points were measured on the sources; see the notes.
set -euo pipefail
cd "$(dirname "$0")/.."

SRC=AudioSource
OUT=Assets/_Game/Audio
# Encoder: OGG Vorbis quality 5 (libvorbis). Uses ffmpeg's libvorbis when it has it; otherwise ffmpeg edits to WAV and
# oggenc (vorbis-tools, the reference libvorbis encoder: `brew install vorbis-tools`) encodes at -q 5.
if ffmpeg -hide_banner -encoders 2>/dev/null | grep -q libvorbis; then USE_OGGENC=0
elif command -v oggenc >/dev/null; then USE_OGGENC=1
else echo "ERROR: need ffmpeg with libvorbis, or oggenc (brew install vorbis-tools)" >&2; exit 1; fi
mkdir -p "$OUT/Music" "$OUT/Ambience" "$OUT/SFX"

calc() { awk "BEGIN { printf \"%.3f\", $1 }"; }
ff() { ffmpeg -hide_banner -loglevel error -y "$@"; }
# encode <ffmpeg input/filter args...> <dst>: runs ffmpeg and writes OGG Vorbis q5 to dst.
encode() {
  local dst="${@: -1}"; local args=("${@:1:$#-1}")
  if [[ $USE_OGGENC == 0 ]]; then ff "${args[@]}" -c:a libvorbis -q:a 5 "$dst"
  else ff "${args[@]}" -map_metadata -1 -fflags +bitexact -flags:a +bitexact -f wav -c:a pcm_s24le - \
         | oggenc -Q -q 5 -o "$dst" - 2> >(grep -vE "LIST|surround channel mask|blindly mapping" >&2 || true); fi
}
need() { [[ -f "$SRC/$1" ]] || { echo "WARNING: missing $SRC/$1 — skipped" >&2; return 1; }; }

# Plain conversion with tiny fades so the file edges never click.
convert() { # src dst
  need "$1" || return 0
  local d; d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$SRC/$1")
  encode -i "$SRC/$1" -af "afade=t=in:d=0.01,afade=t=out:st=$(calc "$d - 0.02"):d=0.02" "$OUT/$2"
}

# Seamless loop of [start, end): the first D seconds are crossfaded with the D seconds that follow `end` in the source,
# so the end of the file flows straight back into its start (equal-power quarter-sine fades).
loop() { # src dst start end crossfade [extra filters, e.g. softening EQ]
  need "$1" || return 0
  local s=$3 e=$4 d=$5 extra=${6:-anull}
  encode -i "$SRC/$1" -filter_complex "\
[0:a]asplit=3[a][b][c];\
[a]atrim=$s:$(calc "$s + $d"),asetpts=PTS-STARTPTS,afade=t=in:d=$d:curve=qsin[head];\
[b]atrim=$e:$(calc "$e + $d"),asetpts=PTS-STARTPTS,afade=t=out:d=$d:curve=qsin[tail];\
[head][tail]amix=inputs=2:normalize=0[seam];\
[c]atrim=$(calc "$s + $d"):$e,asetpts=PTS-STARTPTS[body];\
[seam][body]concat=n=2:v=0:a=1[joined];[joined]$extra[out]" -map "[out]" "$OUT/$2"
}

# One-shot cut of [start, end) with fades.
cut() { # src dst start end fadeIn fadeOut
  need "$1" || return 0
  local len; len=$(calc "$4 - $3")
  encode -i "$SRC/$1" -af "atrim=$3:$4,asetpts=PTS-STARTPTS,afade=t=in:d=$5,afade=t=out:st=$(calc "$len - $6"):d=$6" "$OUT/$2"
}

# ---------------------------------------------------------------- edited files
# Kettle: boil 0.06–14.69 s (the "done" click's transient starts at 14.712 s); the loop seam is a 50 ms crossfade.
# Softened (relaxed, not "dirty"): rumble and hiss rolled off, a touch quieter.
loop sfx_kettle_boil_loop.mp3 SFX/sfx_kettle_boil_loop.ogg 0.06 14.64 0.05 "highpass=f=140,lowpass=f=2600,volume=0.8"
# The click alone: from 20 ms before the transient to the end of the source (only 0.356 s is left; the boil continues under it).
cut  sfx_kettle_boil_loop.mp3 SFX/sfx_kettle_done.ogg 14.692 15.048 0.01 0.03
# Pour: a 9 s one-shot; sound runs 0.33–8.40 s, the rest is silence.
cut  sfx_pour_loop.mp3 SFX/sfx_pour.ogg 0.33 8.40 0.01 0.03
# Spoon: two scoops; the first is 0.17–1.45 s (gap ≈ -55 dB at 1.45–1.65 s, the second starts at 1.70 s).
cut  sfx_spoon_scoop.mp3 SFX/sfx_spoon_scoop.ogg 0.17 1.45 0.01 0.03
# Grinder: 13.0–15.0 s as a seamless loop (10 ms crossfade). Note: the source dips to ≈ -40 dB at 13.6–13.9 s.
loop sfx_grinder_loop.mp3 SFX/sfx_grinder_loop.ogg 13.0 15.0 0.01

# ---------------------------------------------------------------- straight conversions
for f in music_day05 music_day10 music_day20 music_day30; do convert "$f.mp3" "Music/$f.ogg"; done
convert amb_rain_light.mp3          Ambience/amb_rain_light.ogg
# Heavy rain: the source is 10 minutes; one steady minute (388–448 s) as a seamless loop (0.5 s crossfade) saves ~11 MB.
loop amb_rain_heavy.mp3 Ambience/amb_rain_heavy.ogg 388 448 0.5
convert amb_birds.mp3               Ambience/amb_birds.ogg
convert amb_wind.mp3                Ambience/amb_wind.ogg
convert amb_snow.mp3                Ambience/amb_snow.ogg
convert amb_room_fridge.flac        Ambience/amb_room_fridge.ogg
convert amb_room_clock_ticking.wav  SFX/sfx_clock_tick.ogg
# The click is at 0.86 s: cut the silence before it, or the switch sounds late.
cut  sfx_kettle_switch.mp3 SFX/sfx_kettle_switch.ogg 0.84 1.30 0.005 0.08
convert sfx_spoon_tink.mp3          SFX/sfx_spoon_tink.ogg
convert sfx_frother_loop.wav        SFX/sfx_frother_loop.ogg
convert sfx_froth_squeal.mp3        SFX/sfx_froth_squeal.ogg
convert sfx_mug_down.wav            SFX/sfx_mug_down.ogg
convert sfx_perfect.mp3             SFX/sfx_perfect.ogg
convert sfx_door_knock.flac         SFX/sfx_door_knock.ogg
convert sfx_paper.wav               SFX/sfx_paper.ogg
convert sfx_footsteps_leaves.wav    SFX/sfx_footsteps_leaves.ogg
convert sfx_ui_click.mp3            SFX/sfx_ui_click.ogg

echo "Done: $(find "$OUT" -name '*.ogg' | wc -l | tr -d ' ') OGG files in $OUT"
