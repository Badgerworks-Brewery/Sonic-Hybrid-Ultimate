/*
*	rmx Library
*	Copyright (C) 2008-2024 by Eukaryot
*
*	Published under the GNU GPLv3 open source software license, see license.txt
*	or https://www.gnu.org/licenses/gpl-3.0.en.html
*/

#include "rmxmedia.h"

#ifdef RMX_WITH_OPENGL_SUPPORT

namespace opengl
{
	VertexArrayObject::~VertexArrayObject()
	{
		if (mHandle != 0)
		{
			glDeleteVertexArrays(1, &mHandle);
			glDeleteBuffers(1, &mVertexBufferObjectHandle);
		}
	}

	/**
	 * Sets up the Vertex Array Object (VAO) with the specified format.
	 * 
	 * This method initializes or reconfigures the VAO based on the given format.
	 * It handles the creation of necessary OpenGL objects, binding of buffers,
	 * and configuration of vertex attribute pointers. The method supports
	 * different vertex formats including position-only, position-color,
	 * and position-texture coordinates.
	 * 
	 * @param format The format specifying the structure of vertex data.
	 *               Supported formats are:
	 *               - Format::P2: 2D position only
	 *               - Format::P2_C3: 2D position with RGB color
	 *               - Format::P2_C4: 2D position with RGBA color
	 *               - Format::P2_T2: 2D position with 2D texture coordinates
	 * @return void
	 * 
	 * @throws RMX_ERROR If an unrecognized or invalid format is provided.
	 */
	void VertexArrayObject::setup(Format format)
	{
		const bool needsInitialization = (mHandle == 0);
		if (needsInitialization)
		{
			glGenVertexArrays(1, &mHandle);
			glGenBuffers(1, &mVertexBufferObjectHandle);
			glBindVertexArray(mHandle);
			glBindBuffer(GL_ARRAY_BUFFER, mVertexBufferObjectHandle);
		}
		else
		{
			glBindVertexArray(mHandle);
		}

		mCurrentFormat = format;
		switch (format)
		{
			case Format::P2:
			{
				mNumVertexAttributes = 1;
				mFloatsPerVertex = 2;
				glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(0 * sizeof(float)));	// Positions
				break;
			}

			case Format::P2_C3:
			{
				mNumVertexAttributes = 2;
				mFloatsPerVertex = 5;
				glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(0 * sizeof(float)));	// Positions
				glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(2 * sizeof(float)));	// Colors
				break;
			}

			case Format::P2_C4:
			{
				mNumVertexAttributes = 2;
				mFloatsPerVertex = 6;
				glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(0 * sizeof(float)));	// Positions
				glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(2 * sizeof(float)));	// Colors
				break;
			}

			case Format::P2_T2:
			{
				mNumVertexAttributes = 2;
				mFloatsPerVertex = 4;
				glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(0 * sizeof(float)));	// Positions
				glVertexAttribPointer(1, 2, GL_FLOAT, GL_FALSE, (GLsizei)(mFloatsPerVertex * sizeof(float)), (void*)(2 * sizeof(float)));	// Texcoords
				break;
			}

			default:
				RMX_ERROR("Unrecognized or invalid format", );
				break;
		}

		for (size_t i = 0; i < mNumVertexAttributes; ++i)
		{
			glEnableVertexAttribArray((GLuint)i);
		}
		if (!needsInitialization)
		{
			for (size_t i = mNumVertexAttributes; i < 4; ++i)	// Assuming we'll never use more than 4 vertex attributes inside here
			{
				glDisableVertexAttribArray((GLuint)i);
			}
		}
	}

	/**
	 * Updates the vertex data of the Vertex Array Object (VAO).
	 * 
	 * This method updates the vertex buffer associated with the VAO using the provided vertex data.
	 * It binds the VAO and Vertex Buffer Object (VBO), then uploads the new data to the GPU.
	 * The method assumes that the VAO has been previously set up with a valid format.
	 * 
	 * @param vertexData Pointer to the new vertex data to be uploaded
	 * @param numVertices The number of vertices in the provided data
	 * @return void
	 * 
	 * @throws AssertionError if the VAO has not been set up with a format before calling this method
	 */
	void VertexArrayObject::updateVertexData(const float* vertexData, size_t numVertices)
	{
		if (mHandle == 0)
		{
			RMX_ASSERT(false, "VAO must be setup with a format before updating data");
			return;
		}

		glBindVertexArray(mHandle);
		glBindBuffer(GL_ARRAY_BUFFER, mVertexBufferObjectHandle);
		glBufferData(GL_ARRAY_BUFFER, (GLsizeiptr)(mFloatsPerVertex * numVertices * sizeof(GLfloat)), vertexData, GL_STATIC_DRAW);
		mNumBufferedVertices = numVertices;
	}

	/**
	 * Binds the Vertex Array Object (VAO) if it has a valid handle.
	 * 
	 * This method binds the VAO to the OpenGL context if the internal handle (mHandle) is non-zero.
	 * Binding a VAO sets it as the current VAO for subsequent vertex attribute operations.
	 * 
	 * @throws None
	 * @return void
	 */
	void VertexArrayObject::bind()
	{
		if (mHandle != 0)
		{
			glBindVertexArray(mHandle);
		}
	}

	void VertexArrayObject::unbind()
	{
		glBindVertexArray(0);
	}

	/**
	 * Draws the vertex array object using the specified drawing mode.
	 * 
	 * This method binds the vertex array object and draws its vertices using OpenGL.
	 * The drawing will only occur if the handle is valid (non-zero) and there are
	 * vertices buffered.
	 * 
	 * @param mode The OpenGL drawing mode to use (e.g., GL_TRIANGLES, GL_LINES)
	 * @return void
	 */
	void VertexArrayObject::draw(GLenum mode)
	{
		if (mHandle != 0 && mNumBufferedVertices > 0)
		{
			glBindVertexArray(mHandle);
			glDrawArrays(mode, 0, (GLsizei)mNumBufferedVertices);
		}
	}
}

#endif
