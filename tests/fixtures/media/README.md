# Owned synthetic media fixture

`synthetic-pattern.webm` is a 1504-byte VP9 test pattern generated locally from FFmpeg's `testsrc2` filter. It contains no capture, third-party artwork, user content or audio. The G06 browser qualifier uses it to verify actual video decoding/playback; it synthesizes its own PCM WAV tone in memory and keeps preview audio muted.

Generation command:

```sh
ffmpeg -nostdin -loglevel error -f lavfi -i 'testsrc2=size=32x32:rate=8:duration=1' -an -c:v libvpx-vp9 -b:v 30k -y tests/fixtures/media/synthetic-pattern.webm
```

FFmpeg is needed only to regenerate the fixture, not to build or run Windows CI. Scan the file with the deterministic secrets scanner before reading it. CI scans all tracked files before the browser qualifier runs. Real OBS sound output still requires the separate operator check.
