"""Final actual-device screenshots and ten seconds of title motion."""
import sys,time,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from device_qa import Device,OUTPUT
x=Device(sys.argv[1])
x.command('capture-effects-on','motion','on');x.command('capture-ko','language','ko');time.sleep(2)
s=x.command('capture-ko-state','state');x.screenshot('final-ko')
x.tap_control('Language',s);time.sleep(.4);x.command('capture-popup-state','state');x.screenshot('final-language-popup');x.run('shell','input','keyevent','4')
x.command('capture-en','language','en');time.sleep(.5);x.screenshot('final-en')
x.command('capture-ko-return','language','ko');x.command('capture-settings','settings');time.sleep(.5);x.command('capture-settings-state','state');x.screenshot('final-settings');x.run('shell','input','keyevent','4')
x.command('capture-fixture','fixture','on');x.command('capture-fixture-music','music','combat');time.sleep(2);x.screenshot('final-damage-normal')
x.command('capture-fixture-low','motion','off');time.sleep(2);x.screenshot('final-damage-low')
x.command('capture-fixture-settings','settings');time.sleep(.5);x.screenshot('final-ingame-settings');x.run('shell','input','keyevent','4')
x.command('capture-restore-fixture','fixture','off');x.command('capture-restore-motion','motion','on');x.command('capture-restore-music','music','lobby');time.sleep(2)
print('Recording final lobby motion.',flush=True)
remote='/sdcard/lobby-revision2-final.mp4'
x.run('shell','screenrecord','--size','720x1544','--bit-rate','5000000','--time-limit','10',remote)
x.run('pull',remote,str(OUTPUT/'final-lobby-motion.mp4'));x.run('shell','rm','-f',remote)
print(str((OUTPUT/'final-lobby-motion.mp4').resolve()),flush=True)
