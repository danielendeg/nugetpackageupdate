# Background
In cryptography, format-preserving encryption (FPE), refers to encrypting in such a way that the output (the ciphertext) is in the same format as the input (the plaintext). The meaning of "format" varies. Typically only finite domains are discussed, for example:

* To encrypt a 16-digit credit card number so that the ciphertext is another 16-digit number.
* To encrypt an English word so that the ciphertext is another English word.
* To encrypt an n-bit number so that the ciphertext is another n-bit number (this is the definition of an n-bit block cipher).

# Ciphertext Stealing
Ciphertext stealing is a technique for encrypting plaintext using a block cipher, without padding the message to a multiple of the block size, so the ciphertext is **the same size** as the plaintext. \
It does this by altering processing of the last two blocks of the message. The processing of all but the last two blocks is unchanged, but a portion of the second-last block's ciphertext is "stolen" to pad the last plaintext block. The padded final block is then encrypted as usual.


![CipherText_Stealing_(CTS)_on_CBC,_encryption_mode.svg](/.attachments/CipherText_Stealing_(CTS)_on_CBC,_encryption_mode-e2df2017-b7b3-49b8-8bf3-dda0b375efc5.svg)

