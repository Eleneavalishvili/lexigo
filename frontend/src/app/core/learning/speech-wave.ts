const rate = 16000;
const minimumFrames = rate / 10;
const maximumFrames = rate * 30;

export function encodeSpeechWave(channels: readonly Float32Array[], sampleRate: number): Blob {
  const frames = channels[0]?.length ?? 0;
  if (
    sampleRate !== rate ||
    channels.length < 1 ||
    channels.length > 2 ||
    frames < minimumFrames ||
    frames > maximumFrames ||
    channels.some((channel) => channel.length !== frames)
  ) {
    throw new Error('Use a recording between 0.1 and 30 seconds.');
  }
  const bytes = new ArrayBuffer(44 + frames * 2);
  const view = new DataView(bytes);
  const text = (offset: number, value: string) => {
    for (let index = 0; index < value.length; index++)
      view.setUint8(offset + index, value.charCodeAt(index));
  };
  text(0, 'RIFF');
  view.setUint32(4, 36 + frames * 2, true);
  text(8, 'WAVE');
  text(12, 'fmt ');
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, 1, true);
  view.setUint32(24, rate, true);
  view.setUint32(28, rate * 2, true);
  view.setUint16(32, 2, true);
  view.setUint16(34, 16, true);
  text(36, 'data');
  view.setUint32(40, frames * 2, true);
  for (let index = 0; index < frames; index++) {
    let sample = 0;
    for (const channel of channels) {
      if (!Number.isFinite(channel[index]))
        throw new Error('The recording contains invalid audio.');
      sample += channel[index] / channels.length;
    }
    sample = Math.max(-1, Math.min(1, sample));
    view.setInt16(44 + index * 2, Math.round(sample * (sample < 0 ? 32768 : 32767)), true);
  }
  return new Blob([bytes], { type: 'audio/wav' });
}

export async function prepareSpeechWave(recording: Blob, signal: AbortSignal): Promise<Blob> {
  signal.throwIfAborted();
  if (!recording.size || recording.size > 5 * 1024 * 1024)
    throw new Error('Record a shorter phrase and try again.');
  if (typeof OfflineAudioContext === 'undefined')
    throw new Error('This browser cannot prepare speech for checking.');
  const context = new OfflineAudioContext(1, 1, rate);
  const decoded = await context.decodeAudioData(await recording.arrayBuffer());
  signal.throwIfAborted();
  if (decoded.numberOfChannels > 2) throw new Error('Record using a mono or stereo microphone.');
  const channels = Array.from({ length: decoded.numberOfChannels }, (_, index) =>
    decoded.getChannelData(index),
  );
  return encodeSpeechWave(channels, decoded.sampleRate);
}
