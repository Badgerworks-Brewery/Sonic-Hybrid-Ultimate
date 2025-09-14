#ifndef FCASEOPEN_H
#define FCASEOPEN_H

#include <stdio.h>

// Case-insensitive file opening placeholder
// This is a minimal implementation to satisfy the include requirement

#ifdef __cplusplus
extern "C" {
#endif

FILE* fcaseopen(const char* path, const char* mode);

#ifdef __cplusplus
}
#endif

#endif // FCASEOPEN_H
