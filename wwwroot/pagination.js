window.scrollToListStart = (anchor) => {
    if (anchor?.isConnected) {
        anchor.scrollIntoView({ behavior: "instant", block: "start" });
    }
};
