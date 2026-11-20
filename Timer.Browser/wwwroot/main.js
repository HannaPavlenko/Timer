import { dotnet } from "./_framework/dotnet.js";

let audioContext;
let audioReady;

function enableAudio() {
    try {
        audioContext ??= new AudioContext();
        if (audioContext.state !== "running") {
            audioReady = audioContext.resume().catch(console.warn);
        }
    } catch (error) {
        console.warn(error);
    }
}

// Safari grants audio access during a trusted user gesture.
document.addEventListener("touchend", enableAudio, { capture: true, passive: true });
document.addEventListener("pointerup", enableAudio, true);
document.addEventListener("click", enableAudio, true);
document.addEventListener("keydown", enableAudio, true);

async function playWave(wave) {
    if (audioReady && audioContext.state !== "running") {
        await Promise.race([audioReady, new Promise(resolve => setTimeout(resolve, 500))]);
    }

    if (audioContext?.state !== "running") {
        return;
    }

    const bytes = Uint8Array.from(atob(wave), character => character.charCodeAt(0));
    const buffer = await audioContext.decodeAudioData(bytes.buffer);
    const source = audioContext.createBufferSource();
    source.buffer = buffer;
    source.connect(audioContext.destination);

    try {
        await new Promise(resolve => {
            // An interrupted audio session may never dispatch the ended event.
            const timeout = setTimeout(resolve, buffer.duration * 1000 + 500);
            source.onended = () => {
                clearTimeout(timeout);
                resolve();
            };

            try {
                source.start();
            } catch (error) {
                clearTimeout(timeout);
                throw error;
            }
        });
    } finally {
        source.disconnect();
    }
}

try {
    const runtime = await dotnet.create();
    runtime.setModuleImports("stilltime", {
        readStorage: key => localStorage.getItem(key),
        writeStorage: (key, value) => localStorage.setItem(key, value),
        playWave
    });
    await runtime.runMain(runtime.getConfig().mainAssemblyName, [location.href]);
} catch (error) {
    console.error(error);
    const status = document.createElement("p");
    status.className = "avalonia-splash";
    status.setAttribute("role", "alert");
    status.textContent = "Stilltime could not start. Reload the page to try again.";
    document.getElementById("out").replaceChildren(status);
}
