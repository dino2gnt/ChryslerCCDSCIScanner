/*
 * ChryslerCCDSCIScanner (https://github.com/laszlodaniel/ChryslerCCDSCIScanner)
 * Copyright (C) 2018, László Dániel
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * 
 * UART code is based on original library by Andy Gock:
 * https://github.com/andygock/avr-uart
 */

// Board setting: Arduino/Genuino Mega or Mega 2560
// Processor setting: ATmega2560 (Mega 2560)

#include <avr/interrupt.h>
#include <avr/io.h>
#include <avr/pgmspace.h>
#include <avr/wdt.h>
#include <util/atomic.h>
#include <extEEPROM.h>         // https://github.com/JChristensen/extEEPROM
#include <LiquidCrystal_I2C.h> // https://bitbucket.org/fmalpartida/new-liquidcrystal/downloads/
#include <Wire.h>
#include "ccdsciuart.h"

#ifndef F_CPU
#define F_CPU 16000000UL // 16 MHz system clock
#endif

// Construct an object called "eep" for the external 24LC32A EEPROM chip
extEEPROM eep(kbits_32, 1, 32, 0x50); // device size: 32 kilobits = 4 kilobytes, number of devices: 1, page size: 32 bytes (from datasheet), device address: 0x50 by default

// Construct an object called "lcd" for the external display (optional)
LiquidCrystal_I2C lcd(0x27, 2, 1, 0, 4, 5, 6, 7, 3, POSITIVE);

void setup()
{
    // Initialize serial interfaces with default speeds and interrupt control enabled
    usb_init(USBBAUD);// 250000 baud, an external serial monitor should have the same speed
    ccd_init(LOBAUD); // 7812.5 baud
    pcm_init(LOBAUD); // 7812.5 baud
    tcm_init(LOBAUD); // 7812.5 baud

    // Define digital pin states
    // No need to re-define input states...
    pinMode(INT4, INPUT_PULLUP);  // D2 (INT4), CCD-bus idle detector
    pinMode(INT5, INPUT_PULLUP);  // D3 (INT5), CCD-bus active byte detector
    pinMode(RX_LED,  OUTPUT);     // This LED flashes whenever data is received by the scanner
    pinMode(TX_LED,  OUTPUT);     // This LED flashes whenever data is transmitted from the scanner
    // PWR LED is tied to +5V directly, stays on when the scanner has power, draws about 2mA current
    pinMode(ACT_LED, OUTPUT);     // This LED flashes when some "action" takes place in the scanner
    pinMode(BATT, INPUT);
    digitalWrite(RX_LED, HIGH);   // LEDs are grounded through the microcontroller, so HIGH/HI-Z = OFF, LOW = ON
    digitalWrite(TX_LED, HIGH);   // ---||---
    digitalWrite(ACT_LED, HIGH);  // ---||---

    // SCI-bus A/B-configuration selector outputs
    pinMode(PA0, OUTPUT);   // Set PA0 pin to output
    pinMode(PA1, OUTPUT);   // |
    pinMode(PA2, OUTPUT);   // |
    pinMode(PA3, OUTPUT);   // |
    pinMode(PA4, OUTPUT);   // |
    pinMode(PA5, OUTPUT);   // |
    pinMode(PA6, OUTPUT);   // |
    pinMode(PA7, OUTPUT);   // |

//    digitalWrite(PA0, LOW); // Set PA0 low to disable SCI-bus communication by default
//    digitalWrite(PA1, LOW); // |
//    digitalWrite(PA2, LOW); // |
//    digitalWrite(PA3, LOW); // |
//    digitalWrite(PA4, LOW); // |
//    digitalWrite(PA5, LOW); // |
//    digitalWrite(PA6, LOW); // |
//    digitalWrite(PA7, LOW); // |
    
    digitalWrite(PA0, HIGH); // SCI-bus "A" configuration, PCM only
    digitalWrite(PA1, HIGH);
    digitalWrite(PA2, LOW);
    digitalWrite(PA3, LOW);
    digitalWrite(PA4, LOW);
    digitalWrite(PA5, LOW);
    digitalWrite(PA6, LOW);
    digitalWrite(PA7, LOW);

//    digitalWrite(PA0, LOW); // SCI-bus "B" configuration
//    digitalWrite(PA1, LOW);
//    digitalWrite(PA2, LOW);
//    digitalWrite(PA3, LOW);
//    digitalWrite(PA4, HIGH);
//    digitalWrite(PA5, HIGH);
//    digitalWrite(PA6, HIGH);
//    digitalWrite(PA7, HIGH);

    sei(); // enable interrupts, serial interrupt control resumes working
    wdt_enable(WDTO_2S); // enable watchdog timer that resets program if the timer reaches 2 seconds (useful if the prorgam hangs for some reason and needs auto-reset)
    attachInterrupt(digitalPinToInterrupt(INT4), ccd_eom, FALLING); // execute "ccd_eom" function if the CCD-transceiver pulls D2 pin low indicating an "End of Message" condition so the byte reader ISR can flag the next byte as ID-byte
    attachInterrupt(digitalPinToInterrupt(INT5), ccd_active_byte, FALLING); // execute "ccd_active_byte" function if the CCD-transceiver pulls D3 pin low indicating a byte being transmitted on the CCD-bus
    // active byte = we don't know the byte's value right away, we have to wait for all 8 data bits and a few other bits for framing to arrive.
    
    // Initialize external EEPROM
    uint8_t eep_status = eep.begin(extEEPROM::twiClock400kHz); // go fast!
    if (eep_status) { ext_eeprom_present = false; }
    else { ext_eeprom_present = true; }

    // Initialize external display
//    lcd.begin(20, 4); // start LCD with 20 columns and 4 rows
//    lcd.backlight();  // backlight on
//    lcd.clear();      // clear display
//    lcd.home();       // set cursor in home position (0, 0)
//    lcd.print(F("--------------------")); // F(" ") makes the compiler store the string inside flash memory
//    lcd.setCursor(0, 1);
//    lcd.print(F("  CHRYSLER CCD/SCI  "));
//    lcd.setCursor(0, 2);
//    lcd.print(F(" SCANNER V1.40 2018 "));
//    lcd.setCursor(0, 3);
//    lcd.print(F("--------------------"));

    analogReference(DEFAULT); // use default voltage reference applied to AVCC (+5V)
    check_battery_volts(); // calculate battery voltage from OBD16 pin
    ccd_clock_generator(START); // start listening to the CCD-bus

    for (uint8_t i = 0; i < 21; i++) // copy handshake bytes from flash to ram
    {
        handshake_array[i] = pgm_read_byte(&handshake_progmem[i]);
    }

    get_bus_config(); // figure out how to talk to the vehicle
    digitalWrite(ACT_LED, LOW); // flash action LED once to indicate setup is complete and the scanner is ready to accept instructions
    act_led_ontime = millis();
}

void loop()
{
    wdt_reset(); // reset watchdog timer to 0 seconds so no accidental restart occurs
    check_battery_volts(); // calculate battery voltage from OBD16 pin
    handle_usb_data(); // check if a command has been received over the USB connection
    handle_ccd_data(); // do CCD-bus stuff
    handle_sci_data(); // do SCI-bus stuff

    current_millis_blink = millis(); // check current time
    if (heartbeat_enabled) act_led_heartbeat();
    if (current_millis_blink - rx_led_ontime >= led_blink_duration)
    {
        digitalWrite(RX_LED, HIGH); // turn off RX LED
    }
    if (current_millis_blink - tx_led_ontime >= led_blink_duration)
    {
        digitalWrite(TX_LED, HIGH); // turn off TX LED
    }
    if (current_millis_blink - act_led_ontime >= led_blink_duration)
    {
        digitalWrite(ACT_LED, HIGH); // turn off ACT LED
    }
}
