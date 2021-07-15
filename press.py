import datetime
from pynput.keyboard import Key, Controller
keyboard = Controller()




while datetime.datetime.now().hour < 24:
   keyboard.press(Key.enter)
