const childProcess = require('child_process');
const spawn = childProcess.spawn;

childProcess.spawn = function (command, args, options) {
    const isBrowser = command === 'explorer.exe' || command === 'open' || command === 'xdg-open';
    const isDraft = Array.isArray(args) && args.length === 1 &&
        /^https:\/\/yandex\.[^/]+\/games\/app\//.test(args[0]);

    if (isBrowser && isDraft)
        return spawn(process.execPath, ['-e', ''], { stdio: ['ignore', 'ignore', 'pipe'] });

    return spawn.apply(this, arguments);
};
